using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class PhotoGalleryService : IPhotoGalleryService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PhotoGalleryService> _logger;

        public PhotoGalleryService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<PhotoGalleryService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<OperationResult<List<UserPhotoDTO>>> GetUserPhotoGallery(Guid userId)
        {
            try
            {
                var cacheKey = CacheKeys.Users.Photos(userId);
                var cachedPhotos = await _cacheService.GetAsync<List<UserPhotoDTO>>(cacheKey);

                if (cachedPhotos != null)
                {
                    _logger.LogDebug("Returning cached photos for user {UserId}", userId);
                    return OperationResult<List<UserPhotoDTO>>.Successful(cachedPhotos);
                }

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

                // Cache the results
                await _cacheService.SetAsync(cacheKey, photos, CacheKeys.Duration.UserProfile);

                _logger.LogDebug("Retrieved {Count} photos for user {UserId}", photos.Count, userId);
                return OperationResult<List<UserPhotoDTO>>.Successful(photos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving photo gallery for user {UserId}", userId);
                return OperationResult<List<UserPhotoDTO>>.Failed("Unable to load photos. Please try again.");
            }
        }

        public async Task<OperationResult<UserPhotoDTO>> GetPrimaryPhoto(Guid userId)
        {
            try
            {
                var primaryPhoto = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId && p.IsPrimary)
                    .Select(p => new UserPhotoDTO
                    {
                        Id = p.Id,
                        Url = p.Url,
                        ThumbnailUrl = p.ThumbnailUrl,
                        PhotoOrder = p.PhotoOrder,
                        IsPrimary = p.IsPrimary
                    })
                    .FirstOrDefaultAsync();

                if (primaryPhoto == null)
                {
                    return OperationResult<UserPhotoDTO>.NotFound("No primary photo found.");
                }

                return OperationResult<UserPhotoDTO>.Successful(primaryPhoto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving primary photo for user {UserId}", userId);
                return OperationResult<UserPhotoDTO>.Failed("Unable to load primary photo. Please try again.");
            }
        }

        public async Task<OperationResult<int>> GetPhotoCount(Guid userId)
        {
            try
            {
                var count = await _dbContext.UserPhotos.CountAsync(p => p.UserId == userId);
                return OperationResult<int>.Successful(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting photo count for user {UserId}", userId);
                return OperationResult<int>.Failed("Unable to get photo count.");
            }
        }
    }
}