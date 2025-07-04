using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class MatchingService : IMatchingService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly MatchingAlgorithm _matchingAlgorithm;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MatchingService> _logger;

        public MatchingService(MomomiDbContext dbContext, MatchingAlgorithm matchingAlgorithm, ICacheService cacheService, ILogger<MatchingService> logger)
        {
            _dbContext = dbContext;
            _matchingAlgorithm = matchingAlgorithm;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<List<UserProfileDTO>> GetDiscoveryUsersAsync(Guid userId, int count = 10)
        {
            try
            {
                var cacheKey = $"discovery_users_{userId}_{count}";
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null && cachedUsers.Any())
                {
                    _logger.LogInformation("Returning cached discovery users for user {UserId}", userId);
                    return cachedUsers;
                }

                var discoveryUsers = await _matchingAlgorithm.GetPotentialMatchesAsync(userId, count);

                // Cache for 30 minutes
                await _cacheService.SetAsync(cacheKey, discoveryUsers, TimeSpan.FromMinutes(30));

                return discoveryUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery users for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> LikeUserAsync(Guid likerUserId, Guid likedUserId)
        {
            try
            {
                // Check if already liked/passed
                var existingLike = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == likerUserId && ul.LikedUserId == likedUserId);

                if (existingLike != null)
                    return false; // Already liked or passed

                // Create new like record
                var like = new UserLike
                {
                    LikerUserId = likerUserId,
                    LikedUserId = likedUserId,
                    IsLike = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserLikes.Add(like);
                await _dbContext.SaveChangesAsync();

                // Check for match
                var isMatch = await _matchingAlgorithm.CheckForMatchAsync(likerUserId, likedUserId);
                if (isMatch)
                {
                    await _matchingAlgorithm.CreateMatchAsync(likerUserId, likedUserId);
                }

                // Clear discover cache
                await _cacheService.RemoveAsync($"discovery_users_{likerUserId}_10");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking user {LikedUserId} by user {LikerUserId}", likedUserId, likerUserId);
                return false;
            }
        }

        public async Task<bool> PassUserAsync(Guid passerUserId, Guid passedUserId)
        {
            try
            {
                // Check if already liked/passed
                var existingLike = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == passerUserId && ul.LikedUserId == passedUserId);

                if (existingLike != null)
                    return false; // Already liked or passed

                // Create a pass record
                var pass = new UserLike
                {
                    LikerUserId = passerUserId,
                    LikedUserId = passedUserId,
                    IsLike = false, // false indicates a pass
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserLikes.Add(pass);
                await _dbContext.SaveChangesAsync();

                // Clear discover cache
                await _cacheService.RemoveAsync($"discovery_users_{passerUserId}_10");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error passing user {PassedUserId} by user {PasserUserId}", passedUserId, passerUserId);
                return false;
            }
        }

        public async Task<List<MatchDTO>> GetUserMatchesAsync(Guid userId)
        {
            try
            {
                var cacheKey = $"user_matches_{userId}";
                var cachedMatches = await _cacheService.GetAsync<List<MatchDTO>>(cacheKey);

                if (cachedMatches != null)
                {
                    return cachedMatches;
                }

                var matches = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId || ul.LikedUserId == userId) && ul.IsMatch && ul.IsLike)
                    .Include(ul => ul.LikerUser)
                        .ThenInclude(u => u.Photos)
                    .Include(ul => ul.LikedUser)
                        .ThenInclude(u => u.Photos)
                    .OrderByDescending(ul => ul.CreatedAt)
                    .ToListAsync();

                var matchDtos = matches.Select(ul =>
                {
                    var otherUser = ul.LikerUserId == userId ? ul.LikedUser : ul.LikerUser;
                    return new MatchDTO
                    {
                        MatchId = ul.Id,
                        UserId = otherUser.Id,
                        FirstName = otherUser.FirstName,
                        LastName = otherUser.LastName,
                        Age = otherUser.DateOfBirth.HasValue ?
                        DateTime.UtcNow.Year - otherUser.DateOfBirth.Value.Year : 0,
                        PrimaryPhoto = otherUser.Photos.FirstOrDefault(p => p.IsPrimary)?.Url,
                        Heritage = otherUser.Heritage,
                        MatchedAt = ul.CreatedAt,
                        LastMessage = null, // Will be populated when we implement messaging
                        UnreadCount = 0
                    };
                }).ToList();

                // Cache for 15 minutes
                await _cacheService.SetAsync(cacheKey, matchDtos, TimeSpan.FromMinutes(15));

                return matchDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting matches for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> UnmatchAsync(Guid userId, Guid matchedUserId)
        {
            try
            {
                // Find and update like records
                var likes = await _dbContext.UserLikes
                    .Where(ul => ((ul.LikedUserId == userId && ul.LikerUserId == matchedUserId) ||
                                  (ul.LikedUserId == matchedUserId && ul.LikerUserId == userId)) && ul.IsMatch)
                    .ToListAsync();

                foreach (var like in likes)
                {
                    like.IsMatch = false; // Unmatch
                }

                // Deactive conversation
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => (c.User1Id == userId && c.User2Id == matchedUserId) ||
                                              (c.User1Id == matchedUserId && c.User2Id == userId));

                if (conversation != null)
                {
                    conversation.IsActive = false; // Deactivate conversation
                }

                await _dbContext.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"user_matches_{userId}");
                await _cacheService.RemoveAsync($"user_matches_{matchedUserId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmatching user {UserId} from matched user {MatchedUserId}", userId, matchedUserId);
                return false;
            }
        }
    }
}
