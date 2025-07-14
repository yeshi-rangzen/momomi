using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class PhotoManagementService : IPhotoManagementService
    {
        private readonly Cloudinary _cloudinary;
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<PhotoManagementService> _logger;

        private static readonly string[] AllowedMimeTypes = {
            "image/jpeg", "image/jpg", "image/png", "image/webp"
        };

        public PhotoManagementService(
            Cloudinary cloudinary,
            MomomiDbContext dbContext,
            ICacheInvalidation cacheInvalidation,
            ILogger<PhotoManagementService> logger)
        {
            _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cacheInvalidation = cacheInvalidation;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OperationResult<UserPhotoDTO>> AddUserPhoto(Guid userId, IFormFile file, bool setAsPrimary = false)
        {
            try
            {
                _logger.LogInformation("Adding photo for user {UserId}, primary: {SetAsPrimary}", userId, setAsPrimary);

                // Validate file
                var validationResult = ValidatePhotoFile(file);
                if (!validationResult.Success)
                {
                    return OperationResult<UserPhotoDTO>.ValidationFailure(validationResult.ErrorMessage!);
                }

                // Check if user exists
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return OperationResult<UserPhotoDTO>.NotFound("User not found.");
                }

                // Check photo count limit
                var currentPhotoCount = await _dbContext.UserPhotos.CountAsync(p => p.UserId == userId);
                if (currentPhotoCount >= AppConstants.Limits.MaxPhotosPerUser)
                {
                    return OperationResult<UserPhotoDTO>.BusinessRuleViolation(
                        $"Maximum of {AppConstants.Limits.MaxPhotosPerUser} photos allowed per user.");
                }

                // Upload to Cloudinary
                var uploadResult = await UploadToCloudinary(userId, file);
                if (!uploadResult.Success)
                {
                    return OperationResult<UserPhotoDTO>.Failed(uploadResult.ErrorMessage!);
                }

                // If this is the first photo, set it as primary
                if (currentPhotoCount == 0)
                {
                    setAsPrimary = true;
                }

                // If setting as primary, unset other primary photos
                if (setAsPrimary)
                {
                    await UnsetOtherPrimaryPhotos(userId);
                }

                // Create photo record
                var userPhoto = new UserPhoto
                {
                    UserId = userId,
                    CloudinaryPublicId = uploadResult.Data!.PublicId,
                    Url = uploadResult.Data.SecureUrl.ToString(),
                    ThumbnailUrl = GenerateThumbnailUrl(uploadResult.Data.SecureUrl.ToString()),
                    PhotoOrder = currentPhotoCount,
                    IsPrimary = setAsPrimary,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserPhotos.Add(userPhoto);
                await _dbContext.SaveChangesAsync();

                // Clear caches
                await _cacheInvalidation.InvalidateUserProfile(userId);

                var photoDto = MapToPhotoDTO(userPhoto);

                _logger.LogInformation("Successfully added photo {PhotoId} for user {UserId}", userPhoto.Id, userId);
                return OperationResult<UserPhotoDTO>.Successful(photoDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding photo for user {UserId}", userId);
                return OperationResult<UserPhotoDTO>.Failed("Failed to upload photo. Please try again.");
            }
        }

        public async Task<OperationResult> RemovePhoto(Guid userId, Guid photoId)
        {
            try
            {
                _logger.LogInformation("Removing photo {PhotoId} for user {UserId}", photoId, userId);

                var photo = await _dbContext.UserPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId);

                if (photo == null)
                {
                    return OperationResult.NotFound("Photo not found or you don't have permission to delete it.");
                }

                // Delete from Cloudinary
                var deleteResult = await DeleteFromCloudinary(photo.CloudinaryPublicId);
                if (!deleteResult.Success)
                {
                    _logger.LogWarning("Failed to delete photo from Cloudinary: {Error}", deleteResult.ErrorMessage);
                    // Continue with database deletion even if Cloudinary deletion fails
                }

                // If this was the primary photo, set another as primary
                if (photo.IsPrimary)
                {
                    await SetNewPrimaryPhoto(userId, photoId);
                }

                // Remove from database
                _dbContext.UserPhotos.Remove(photo);
                await _dbContext.SaveChangesAsync();

                // Clear caches
                await _cacheInvalidation.InvalidateUserProfile(userId);

                _logger.LogInformation("Successfully removed photo {PhotoId} for user {UserId}", photoId, userId);
                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing photo {PhotoId} for user {UserId}", photoId, userId);
                return OperationResult.Failed("Failed to remove photo. Please try again.");
            }
        }

        public async Task<OperationResult> SetPrimaryPhoto(Guid userId, Guid photoId)
        {
            try
            {
                _logger.LogInformation("Setting primary photo {PhotoId} for user {UserId}", photoId, userId);

                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                var targetPhoto = photos.FirstOrDefault(p => p.Id == photoId);
                if (targetPhoto == null)
                {
                    return OperationResult.NotFound("Photo not found or you don't have permission to modify it.");
                }

                // Unset all primary flags and set target as primary
                foreach (var photo in photos)
                {
                    photo.IsPrimary = photo.Id == photoId;
                }

                await _dbContext.SaveChangesAsync();

                // Clear caches
                await _cacheInvalidation.InvalidateUserProfile(userId);

                _logger.LogInformation("Successfully set primary photo {PhotoId} for user {UserId}", photoId, userId);
                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary photo {PhotoId} for user {UserId}", photoId, userId);
                return OperationResult.Failed("Failed to set primary photo. Please try again.");
            }
        }

        public async Task<OperationResult> ReorderPhotos(Guid userId, List<Guid> orderedPhotoIds)
        {
            try
            {
                _logger.LogInformation("Reordering photos for user {UserId}", userId);

                if (orderedPhotoIds == null || !orderedPhotoIds.Any())
                {
                    return OperationResult.ValidationFailure("Photo IDs list cannot be empty.");
                }

                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId && orderedPhotoIds.Contains(p.Id))
                    .ToListAsync();

                if (photos.Count != orderedPhotoIds.Count)
                {
                    return OperationResult.ValidationFailure("One or more photo IDs are invalid.");
                }

                // Update photo order
                for (int i = 0; i < orderedPhotoIds.Count; i++)
                {
                    var photo = photos.FirstOrDefault(p => p.Id == orderedPhotoIds[i]);
                    if (photo != null)
                    {
                        photo.PhotoOrder = i;
                    }
                }

                await _dbContext.SaveChangesAsync();

                // Clear caches
                await _cacheInvalidation.InvalidateUserProfile(userId);

                _logger.LogInformation("Successfully reordered photos for user {UserId}", userId);
                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering photos for user {UserId}", userId);
                return OperationResult.Failed("Failed to reorder photos. Please try again.");
            }
        }

        // Private helper methods
        private static OperationResult ValidatePhotoFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return OperationResult.ValidationFailure("No file uploaded.");
            }

            if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return OperationResult.ValidationFailure("Invalid file type. Only JPEG, PNG, and WebP images are allowed.");
            }

            if (file.Length > AppConstants.FileSizes.MaxPhotoSizeBytes)
            {
                return OperationResult.ValidationFailure($"File size exceeds {AppConstants.FileSizes.MaxPhotoSizeBytes / (1024 * 1024)}MB limit.");
            }

            return OperationResult.Successful();
        }

        private async Task<OperationResult<ImageUploadResult>> UploadToCloudinary(Guid userId, IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"mimori/users/{userId}",
                    PublicId = $"user_{userId}_{Guid.NewGuid()}",
                    Transformation = new Transformation()
                        .Width(AppConstants.FileSizes.DisplayPhotoSize)
                        .Height(AppConstants.FileSizes.DisplayPhotoSize)
                        .Crop("fill")
                        .Quality("auto")
                        .FetchFormat("auto"),
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    _logger.LogError("Cloudinary upload error: {Error}", uploadResult.Error.Message);
                    return OperationResult<ImageUploadResult>.Failed("Photo upload failed. Please try again.");
                }

                return OperationResult<ImageUploadResult>.Successful(uploadResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to Cloudinary");
                return OperationResult<ImageUploadResult>.Failed("Photo upload failed. Please try again.");
            }
        }

        private async Task<OperationResult> DeleteFromCloudinary(string publicId)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId);
                var deleteResult = await _cloudinary.DestroyAsync(deleteParams);

                if (deleteResult.Error != null)
                {
                    return OperationResult.Failed($"Cloudinary deletion error: {deleteResult.Error.Message}");
                }

                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting from Cloudinary: {PublicId}", publicId);
                return OperationResult.Failed("Failed to delete photo from storage.");
            }
        }

        private async Task UnsetOtherPrimaryPhotos(Guid userId)
        {
            var primaryPhotos = await _dbContext.UserPhotos
                .Where(p => p.UserId == userId && p.IsPrimary)
                .ToListAsync();

            foreach (var photo in primaryPhotos)
            {
                photo.IsPrimary = false;
            }
        }

        private async Task SetNewPrimaryPhoto(Guid userId, Guid excludePhotoId)
        {
            var nextPhoto = await _dbContext.UserPhotos
                .Where(p => p.UserId == userId && p.Id != excludePhotoId)
                .OrderBy(p => p.PhotoOrder)
                .FirstOrDefaultAsync();

            if (nextPhoto != null)
            {
                nextPhoto.IsPrimary = true;
            }
        }

        private static string GenerateThumbnailUrl(string originalUrl)
        {
            // Generate thumbnail URL using Cloudinary transformations
            return originalUrl.Replace("/upload/", $"/upload/w_{AppConstants.FileSizes.ThumbnailSize},h_{AppConstants.FileSizes.ThumbnailSize},c_fill/");
        }

        private static UserPhotoDTO MapToPhotoDTO(UserPhoto photo)
        {
            return new UserPhotoDTO
            {
                Id = photo.Id,
                Url = photo.Url,
                ThumbnailUrl = photo.ThumbnailUrl,
                PhotoOrder = photo.PhotoOrder,
                IsPrimary = photo.IsPrimary
            };
        }
    }
}