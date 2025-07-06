using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class MatchingService : IMatchingService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly MatchingAlgorithm _matchingAlgorithm;
        private readonly ICacheService _cacheService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IBlockingService _blockingService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<MatchingService> _logger;

        public MatchingService(
           MomomiDbContext dbContext,
           MatchingAlgorithm matchingAlgorithm,
           ICacheService cacheService,
           ISubscriptionService subscriptionService,
           IBlockingService blockingService,
           IPushNotificationService pushNotificationService,
           ILogger<MatchingService> logger)
        {
            _dbContext = dbContext;
            _matchingAlgorithm = matchingAlgorithm;
            _cacheService = cacheService;
            _subscriptionService = subscriptionService;
            _blockingService = blockingService;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public async Task<List<UserProfileDTO>> GetDiscoveryUsersAsync(Guid userId, int count = 10)
        {
            try
            {
                // Get current user to check discovery preference
                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser == null)
                {
                    _logger.LogWarning("User not found for discovery: {UserId}", userId);
                    return [];
                }

                // Create different cache keys for global vs local discovery
                var discoveryMode = currentUser.EnableGlobalDiscovery ? "global" : "local";
                var cacheKey = $"discovery_users_{userId}_{discoveryMode}_{count}";
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null && cachedUsers.Any())
                {
                    _logger.LogInformation("Returning cached discovery users for user {UserId}", userId);
                    return cachedUsers;
                }

                var discoveryUsers = await _matchingAlgorithm.GetPotentialMatchesAsync(userId, count);

                // Filter out blocked users
                var filteredUsers = new List<UserProfileDTO>();
                foreach (var user in discoveryUsers)
                {
                    var isBlocked = await _blockingService.IsUserBlockedAsync(userId, user.Id);
                    if (!isBlocked)
                    {
                        filteredUsers.Add(user);
                    }
                }

                // Cache for shorter time for global discovery to ensure fresh results
                var cacheTime = currentUser.EnableGlobalDiscovery ?
                    TimeSpan.FromMinutes(15) : // Global: 15 minutes
                    TimeSpan.FromMinutes(30);  // Local: 30 minutes

                await _cacheService.SetAsync(cacheKey, filteredUsers, cacheTime);

                return filteredUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery users for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> LikeUserAsync(Guid likerUserId, Guid likedUserId, LikeType likeType = LikeType.Regular)
        {
            try
            {
                // Check if user can like (subscription limits)
                var canLike = await _subscriptionService.CanUserLikeAsync(likerUserId, likeType);
                if (!canLike)
                {
                    _logger.LogWarning("User {UserId} exceeded like limits for {LikeType}", likerUserId, likeType);
                    return false;
                }

                // Check if users are blocked
                var isBlocked = await _blockingService.IsUserBlockedAsync(likerUserId, likedUserId);
                if (isBlocked)
                {
                    _logger.LogWarning("Cannot like blocked user. Liker: {LikerUserId}, Liked: {LikedUserId}",
                        likerUserId, likedUserId);
                    return false;
                }

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

                // Record usage
                await _subscriptionService.RecordLikeUsageAsync(likerUserId, likeType);

                // Send super like notification if applicable
                if (likeType == LikeType.SuperLike)
                {
                    await _pushNotificationService.SendSuperLikeNotificationAsync(likedUserId, likerUserId);
                }

                await _dbContext.SaveChangesAsync();

                // Check for match
                var isMatch = await _matchingAlgorithm.CheckForMatchAsync(likerUserId, likedUserId);
                if (isMatch)
                {
                    await _matchingAlgorithm.CreateMatchAsync(likerUserId, likedUserId);

                    // Send match notifications to the liked user
                    await _pushNotificationService.SendMatchNotificationAsync(likedUserId, likerUserId);

                    _logger.LogInformation("Match created between users {User1} and {User2}",
                        likerUserId, likedUserId);
                }

                // Clear discovery cache for both global and local modes
                await ClearDiscoveryCacheAsync(likerUserId);

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

                // Clear discovery cache for both global and local modes
                await ClearDiscoveryCacheAsync(passerUserId);

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
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
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

                    // Find and DELETE conversation and all messages
                    var conversation = await _dbContext.Conversations
                        .Include(c => c.Messages)
                        .FirstOrDefaultAsync(c => (c.User1Id == userId && c.User2Id == matchedUserId) ||
                                                  (c.User1Id == matchedUserId && c.User2Id == userId));

                    if (conversation != null)
                    {
                        // Delete all messages first (due to foreign key constraints)
                        _dbContext.Messages.RemoveRange(conversation.Messages);

                        // Delete the conversation
                        _dbContext.Conversations.Remove(conversation);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Clear cache
                    await _cacheService.RemoveAsync($"user_matches_{userId}");
                    await _cacheService.RemoveAsync($"user_matches_{matchedUserId}");
                    await _cacheService.RemoveAsync($"user_conversations_{userId}");
                    await _cacheService.RemoveAsync($"user_conversations_{matchedUserId}");

                    _logger.LogInformation("Successfully unmatched users {UserId} and {MatchedUserId} and deleted conversation", userId, matchedUserId);

                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmatching user {UserId} from matched user {MatchedUserId}", userId, matchedUserId);
                return false;
            }
        }

        /// <summary>
        /// Helper method to clear discovery cache for both modes
        /// </summary>
        private async Task ClearDiscoveryCacheAsync(Guid userId)
        {
            try
            {
                // Clear cache for both discovery modes
                await _cacheService.RemoveAsync($"discovery_users_{userId}_global_10");
                await _cacheService.RemoveAsync($"discovery_users_{userId}_local_10");

                // Also clear with different counts if you use variable counts
                for (int count = 5; count <= 20; count += 5)
                {
                    await _cacheService.RemoveAsync($"discovery_users_{userId}_global_{count}");
                    await _cacheService.RemoveAsync($"discovery_users_{userId}_local_{count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing discovery cache for user {UserId}", userId);
                // Don't throw - cache clearing failure shouldn't break the main operation
            }
        }
    }
}
