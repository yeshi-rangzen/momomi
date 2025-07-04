using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class CloudinaryPhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<CloudinaryPhotoService> _logger;

        public CloudinaryPhotoService(Cloudinary cloudinary, MomomiDbContext dbContext, ILogger<CloudinaryPhotoService> logger)
        {
            _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary), "Cloudinary instance cannot be null.");
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext), "Database context cannot be null.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        }

        public async Task<UserPhotoDTO?> UploadPhotoAsync(Guid userId, IFormFile file, bool isPrimary = false)
        {
            try
            {
                if (file == null || file.Length == 0) return null;

                // Check if user exists
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return null;

                // Check the photo limit
                var photoCount = await _dbContext.UserPhotos.CountAsync(p => p.UserId == userId);
                if (photoCount >= 6)
                {
                    return null;
                }

                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"momomi/users/{userId}",
                    PublicId = $"user_{userId}_{Guid.NewGuid()}",
                    Transformation = new Transformation().Width(800).Height(800).Crop("fill").Quality("auto").FetchFormat("auto"),
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    _logger.LogError("UploadPhotoAsync: Error uploading photo - {ErrorMessage}", uploadResult.Error.Message);
                    return null;
                }

                // If this is the first photo, set it as primary
                if (photoCount == 0) isPrimary = true;
                // Unset other primary photos if this one is primary
                if (isPrimary)
                {
                    var existingPrimary = await _dbContext.UserPhotos.Where(p => p.UserId == userId && p.IsPrimary).ToListAsync();
                    foreach (var photo in existingPrimary)
                    {
                        photo.IsPrimary = false;
                    }
                }

                var userPhoto = new UserPhoto
                {
                    UserId = userId,
                    CloudinaryPublicId = uploadResult.PublicId,
                    Url = uploadResult.SecureUrl.ToString(),
                    ThumbnailUrl = uploadResult.SecureUrl.ToString(), // Assuming thumbnail is same for now
                    PhotoOrder = photoCount,
                    IsPrimary = isPrimary,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserPhotos.Add(userPhoto);
                await _dbContext.SaveChangesAsync();

                // Return DTO instead of entity
                return new UserPhotoDTO
                {
                    Id = userPhoto.Id,
                    Url = userPhoto.Url,
                    ThumbnailUrl = userPhoto.ThumbnailUrl,
                    PhotoOrder = userPhoto.PhotoOrder,
                    IsPrimary = userPhoto.IsPrimary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadPhotoAsync: An error occurred while uploading photo for user {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> DeletePhotoAsync(Guid photoId, Guid userId)
        {
            try
            {
                var photo = await _dbContext.UserPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId);
                if (photo == null) return false;

                // Delete from Cloudinary
                var deleteParams = new DeletionParams(photo.CloudinaryPublicId);
                var deleteResult = await _cloudinary.DestroyAsync(deleteParams);

                if (deleteResult.Error != null)
                {
                    _logger.LogError("DeletePhotoAsync: Error deleting photo from Cloudinary - {ErrorMessage}", deleteResult.Error.Message);
                    return false;
                }

                // If this was the primary photo, set another as primary
                if (photo.IsPrimary)
                {
                    var nextPhoto = await _dbContext.UserPhotos.Where(p => p.UserId == userId && p.Id != photoId)
                        .OrderBy(p => p.PhotoOrder)
                        .FirstOrDefaultAsync();

                    if (nextPhoto != null)
                    {
                        nextPhoto.IsPrimary = true;
                    }
                }

                // Remove photo from database
                _dbContext.UserPhotos.Remove(photo);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletePhotoAsync: An error occurred while deleting photo {PhotoId} for user {UserId}", photoId, userId);
                return false;
            }
        }

        public async Task<bool> SetPrimaryPhotoAsync(Guid photoId, Guid userId)
        {
            try
            {
                var photos = await _dbContext.UserPhotos
                                    .Where(p => p.UserId == userId)
                                    .ToListAsync();

                var targetPhoto = photos.FirstOrDefault(p => p.Id == photoId);
                if (targetPhoto == null) return false;

                // Unset all primary flags
                foreach (var photo in photos)
                {
                    photo.IsPrimary = photo.Id == photoId;
                }

                // Set this photo as primary
                targetPhoto.IsPrimary = true;
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetPrimaryPhotoAsync: An error occurred while setting primary photo {PhotoId} for user {UserId}", photoId, userId);
                return false;
            }
        }

        public async Task<IEnumerable<UserPhotoDTO>> GetUserPhotosAsync(Guid userId)
        {
            try
            {
                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId)
                    .OrderBy(p => p.PhotoOrder)
                    .Select(p => new UserPhotoDTO
                    {
                        Id = p.Id,
                        Url = p.Url,
                        ThumbnailUrl = p.ThumbnailUrl,
                        PhotoOrder = p.PhotoOrder,
                        IsPrimary = p.IsPrimary
                    })
                    .ToListAsync();

                return photos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserPhotosAsync: An error occurred while retrieving photos for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> ReorderPhotosAsync(Guid userId, List<Guid> photoIds)
        {
            try
            {
                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId && photoIds.Contains(p.Id))
                    .ToListAsync();

                if (photos.Count != photoIds.Count) return false; // Not all photos found

                for (int i = 0; i < photoIds.Count; i++)
                {
                    var photo = photos.FirstOrDefault(p => p.Id == photoIds[i]);
                    if (photo != null)
                    {
                        photo.PhotoOrder = i;
                    }
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReorderPhotosAsync: An error occurred while reordering photos for user {UserId}", userId);
                return false;
            }
        }

    }
}
