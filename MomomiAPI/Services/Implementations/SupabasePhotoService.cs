using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace MomomiAPI.Services.Implementations
{
    public class SupabasePhotoService : IPhotoManagementService
    {
        private readonly Supabase.Client _adminClient;
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<SupabasePhotoService> _logger;
        private readonly IConfiguration _configuration;

        private static readonly string[] AllowedMimeTypes =
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp"
        };

        private const string UserPhotosBucket = "user-photos";

        public SupabasePhotoService(
            [FromKeyedServices("AdminClient")] Supabase.Client adminClient,
            MomomiDbContext dbContext,
            ICacheInvalidation cacheInvalidation,
            ILogger<SupabasePhotoService> logger,
            IConfiguration configuration
            )
        {
            _adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cacheInvalidation = cacheInvalidation;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _configuration = configuration;
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
                    return OperationResult<UserPhotoDTO>.ValidationFailure(validationResult.ErrorMessage!);
                }

                // Check photo count limit
                var currentPhotoCount = await _dbContext.UserPhotos.CountAsync(p => p.UserId == userId);
                if (currentPhotoCount >= AppConstants.Limits.MaxPhotosPerUser)
                {
                    return OperationResult<UserPhotoDTO>.BusinessRuleViolation(
                        $"Maximum of {AppConstants.Limits.MaxPhotosPerUser} photos allowed per user.");
                }

                // Upload to Supabase Storage
                var uploadResult = await UploadToSupabaseStorage(userId, file);
                if (!uploadResult.Success)
                {
                    return OperationResult<UserPhotoDTO>.Failed(uploadResult.ErrorMessage!);
                }

                // If this is the first photo, set it as primary
                if (currentPhotoCount == 0)
                {
                    setAsPrimary = true;
                }

                // Create photo record
                var userPhoto = new UserPhoto
                {
                    UserId = userId,
                    StoragePath = uploadResult.Data!.FilePath,
                    Url = uploadResult.Data.PublicUrl,
                    ThumbnailUrl = GenerateThumbnailUrl(uploadResult.Data.PublicUrl),
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
                _logger.LogInformation("Removing photo {PhotoId} for {UserId}", photoId, userId);

                var photo = await _dbContext.UserPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId);

                if (photo == null)
                {
                    return OperationResult.NotFound("Photo not found or you don't have permission to delete.");
                }

                // Delete from Supabase Storage
                var deleteResult = await DeleteFromSupabaseStorage(photo.StoragePath);
                if (!deleteResult.Success)
                {
                    _logger.LogWarning("Failed to delete photo from Supabase Storage: {Error}", deleteResult.ErrorMessage);
                    // Continue with database deletion even if storage deletion failes
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
                _logger.LogError(ex, "Error removing {PhotoId} for user {UserId}", photoId, userId);
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
                    return OperationResult.ValidationFailure("Photo IDs list cannot be empty");
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
                return OperationResult.ValidationFailure("Invalid file type. Only JPEG, PNG and WebP images are allowed.");
            }

            if (file.Length > AppConstants.FileSizes.MaxPhotoSizeBytes)
            {
                return OperationResult.ValidationFailure($"File size exceeds {AppConstants.FileSizes.MaxPhotoSizeBytes / (1024 * 1024)}MB limit.");
            }

            return OperationResult.Successful();
        }

        private async Task<OperationResult<SupabaseUploadResult>> UploadToSupabaseStorage(Guid userId, IFormFile file)
        {
            try
            {
                // Generate unique filename
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"user_{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = $"users/{userId}/{fileName}";

                // Process and resize image
                using var processedImageStream = await ProcessImageAsync(file);
                var fileBytes = processedImageStream.ToArray();

                // Upload to Supabase Storage
                var uploadResult = await _adminClient.Storage
                    .From(UserPhotosBucket)
                    .Upload(fileBytes, filePath, new Supabase.Storage.FileOptions
                    {
                        ContentType = file.ContentType,
                        Upsert = false
                    });

                if (uploadResult == null)
                {
                    return OperationResult<SupabaseUploadResult>.Failed("Failed to upload photo to storage.");
                }

                // Get public URL
                var publicUrl = _adminClient.Storage.From(UserPhotosBucket).GetPublicUrl(filePath);

                var result = new SupabaseUploadResult
                {
                    FilePath = filePath,
                    PublicUrl = publicUrl,
                    FileName = fileName
                };

                return OperationResult<SupabaseUploadResult>.Successful(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to Supabase Storage");
                return OperationResult<SupabaseUploadResult>.Failed("Photo upload failed. Please try again later.");
            }
        }

        private async Task<OperationResult> DeleteFromSupabaseStorage(string filePath)
        {
            try
            {
                var deleteResult = await _adminClient.Storage
                    .From(UserPhotosBucket)
                    .Remove(new List<string> { filePath });

                if (deleteResult == null || !deleteResult.Any())
                {
                    return OperationResult.Failed("Failed to delete photo from storage.");
                }

                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting from Supabase Storage: {FilePath}", filePath);
                return OperationResult.Failed("Failed to delete photo from storage.");
            }
        }

        private async Task<MemoryStream> ProcessImageAsync(IFormFile file)
        {
            try
            {

                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream);

                // Calculate resize dimensions while maintaining aspect ratio
                var (newWidth, newHeight) = CalculateResizeDimensions(
                    image.Width, image.Height,
                    AppConstants.FileSizes.DisplayPhotoSize,
                    AppConstants.FileSizes.DisplayPhotoSize);

                // Resize the image
                image.Mutate(x => x.Resize(newWidth, newHeight));

                // save to memory stream with appropriate encoder
                var outputStream = new MemoryStream();
                var encoder = GetImageEncoder(file.ContentType);

                await image.SaveAsync(outputStream, encoder);
                outputStream.Position = 0;
                return outputStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image");
                throw new InvalidOperationException("Failed to process image", ex);
            }
        }

        private static (int width, int height) CalculateResizeDimensions(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / originalWidth;
            var ratioY = (double)maxHeight / originalHeight;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(originalWidth * ratio);
            var newHeight = (int)(originalHeight * ratio);

            return (newWidth, newHeight);
        }

        private static IImageEncoder GetImageEncoder(string mimeType)
        {
            return mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => new JpegEncoder { Quality = 85 },
                "image/png" => new PngEncoder(),
                "image/webp" => new WebpEncoder { Quality = 85 },
                _ => new JpegEncoder { Quality = 85 } // Default to JPEG
            };
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

        private string GenerateThumbnailUrl(string originalUrl)
        {
            // For Supabase, we'll use URL parameters for transformation
            // This assumes you have image transformations enabled in Supabase
            return $"{originalUrl}?width={AppConstants.FileSizes.ThumbnailSize}&height={AppConstants.FileSizes.ThumbnailSize}&resize=cover";
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

        // Helper class for upload results
        private class SupabaseUploadResult
        {
            public string FilePath { get; set; } = string.Empty;
            public string PublicUrl { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }
    }
}
