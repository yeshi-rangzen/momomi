using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class UserInteractionService : IUserInteractionService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IReportingService _reportingService;
        private readonly IMatchManagementService _matchManagementService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<UserInteractionService> _logger;

        public UserInteractionService(
            MomomiDbContext dbContext,
            ISubscriptionService subscriptionService,
            IReportingService reportingService,
            IMatchManagementService matchManagementService,
            IPushNotificationService pushNotificationService,
            ICacheInvalidation cacheInvalidation,
            ILogger<UserInteractionService> logger)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
            _reportingService = reportingService;
            _matchManagementService = matchManagementService;
            _pushNotificationService = pushNotificationService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
        }

        public async Task<InteractionResult> ExpressInterest(Guid likerId, Guid likedId, LikeType interestType = LikeType.Regular)
        {
            try
            {
                _logger.LogInformation("User {LikerId} expressing {InterestType} interest in user {LikedId}",
                    likerId, interestType, likedId);

                // Check if user can like based on subscription limits
                var canLikeResult = await _subscriptionService.CanUserLikeAsync(likerId, interestType);
                if (!canLikeResult.Success)
                {
                    return InteractionResult.Failed("Unable to check like permissions.");
                }

                if (!canLikeResult.Data)
                {
                    var usageLimitsResult = await _subscriptionService.GetUsageLimitsAsync(likerId);
                    var usageLimits = usageLimitsResult.Success ? usageLimitsResult.Data : null;
                    var limitType = interestType == LikeType.SuperLike ? "super likes" : "likes";
                    return InteractionResult.LimitReached($"You've reached your daily {limitType} limit.", usageLimits, interestType);
                }

                // Check if user is reported
                var isReportedResult = await _reportingService.IsUserReportedAsync(likerId, likedId);
                if (!isReportedResult.Success)
                {
                    _logger.LogWarning("Unable to check report status for user {LikerId} and {LikedId}", likerId, likedId);
                    return InteractionResult.Failed("Unable to verify user status.");
                }

                if (isReportedResult.Data)
                {
                    return InteractionResult.UserBlocked(likedId);
                }

                // Check if already liked/passed
                var existingInteraction = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == likerId && ul.LikedUserId == likedId);

                if (existingInteraction != null)
                {
                    return InteractionResult.UserAlreadyProcessed(likedId);
                }

                // Verify target user exists and is active
                var targetUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == likedId && u.IsActive);

                if (targetUser == null)
                {
                    return InteractionResult.UserNotFound();
                }

                // Create new like record
                var like = new UserLike
                {
                    LikerUserId = likerId,
                    LikedUserId = likedId,
                    IsLike = true,
                    LikeType = interestType,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserLikes.Add(like);

                // Record usage
                var recordUsageResult = await _subscriptionService.RecordLikeUsageAsync(likerId, interestType);
                if (!recordUsageResult.Success)
                {
                    _logger.LogWarning("Failed to record like usage for user {LikerId}", likerId);
                }

                // Check for match
                var matchResult = await _matchManagementService.CheckForNewMatch(likerId, likedId);
                var isMatch = matchResult.Success && matchResult.Data;

                if (isMatch)
                {
                    like.IsMatch = true;
                }

                await _dbContext.SaveChangesAsync();

                // Send notifications
                if (interestType == LikeType.SuperLike)
                {
                    await _pushNotificationService.SendSuperLikeNotificationAsync(likedId, likerId);
                }

                if (isMatch)
                {
                    await _pushNotificationService.SendMatchNotificationAsync(likedId, likerId);
                    _logger.LogInformation("Match created between users {User1} and {User2}", likerId, likedId);
                }

                // Clear relevant caches
                await _cacheInvalidation.InvalidateUserDiscovery(likerId);

                if (isMatch)
                {
                    await _cacheInvalidation.InvalidateMatchingCaches(likerId, likedId);
                }

                // Get updated usage limits
                var updatedLimitsResult = await _subscriptionService.GetUsageLimitsAsync(likerId);
                var updatedLimits = updatedLimitsResult.Success ? updatedLimitsResult.Data : null;

                return isMatch
                    ? InteractionResult.MatchCreated(likedId, interestType, updatedLimits!)
                    : InteractionResult.LikeRecorded(likedId, interestType, updatedLimits!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expressing interest from user {LikerId} to user {LikedId}", likerId, likedId);
                return InteractionResult.Failed("Unable to process your like. Please try again.");
            }
        }

        public async Task<InteractionResult> DismissUser(Guid dismisserId, Guid dismissedId)
        {
            try
            {
                _logger.LogInformation("User {DismisserId} dismissing user {DismissedId}", dismisserId, dismissedId);

                // Check if already liked/passed
                var existingInteraction = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == dismisserId && ul.LikedUserId == dismissedId);

                if (existingInteraction != null)
                {
                    return InteractionResult.UserAlreadyProcessed(dismissedId);
                }

                // Verify target user exists
                var targetUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == dismissedId && u.IsActive);

                if (targetUser == null)
                {
                    return InteractionResult.UserNotFound();
                }

                // Create a pass record
                var pass = new UserLike
                {
                    LikerUserId = dismisserId,
                    LikedUserId = dismissedId,
                    IsLike = false, // false indicates a pass
                    LikeType = LikeType.Regular,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserLikes.Add(pass);
                await _dbContext.SaveChangesAsync();

                // Clear discovery cache
                await _cacheInvalidation.InvalidateUserDiscovery(dismisserId);

                _logger.LogDebug("User {DismisserId} successfully dismissed user {DismissedId}", dismisserId, dismissedId);
                return InteractionResult.PassRecorded(dismissedId)
                    .WithMetadata("dismissed_at", DateTime.UtcNow) as InteractionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing user {DismissedId} by user {DismisserId}", dismissedId, dismisserId);
                return InteractionResult.Failed("Unable to dismiss user. Please try again.");
            }
        }

        public async Task<InteractionResult> UndoLastSwipe(Guid userId)
        {
            try
            {
                _logger.LogInformation("User {UserId} attempting to undo last swipe", userId);

                // Find the most recent like/pass by this user
                var lastSwipe = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .OrderByDescending(ul => ul.CreatedAt)
                    .FirstOrDefaultAsync();

                if (lastSwipe == null)
                {
                    return InteractionResult.Failed("No recent swipe found to undo.");
                }

                // Check if it's been too long (only allow undo within 5 minutes)
                if (DateTime.UtcNow - lastSwipe.CreatedAt > TimeSpan.FromMinutes(5))
                {
                    return InteractionResult.Failed("Cannot undo a swipe older than 5 minutes.");
                }

                // Check if it resulted in a match - matches cannot be undone this way
                if (lastSwipe.IsMatch)
                {
                    return InteractionResult.Failed("Cannot undo a swipe that resulted in a match.");
                }

                // Remove the swipe record
                _dbContext.UserLikes.Remove(lastSwipe);
                await _dbContext.SaveChangesAsync();

                // Clear discovery cache to potentially show the user again
                await _cacheInvalidation.InvalidateUserDiscovery(userId);

                _logger.LogInformation("User {UserId} successfully undid last swipe on user {TargetUserId}",
                    userId, lastSwipe.LikedUserId);

                return InteractionResult.UndoSuccessful(lastSwipe.LikedUserId)
                    .WithMetadata("undone_at", DateTime.UtcNow) as InteractionResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error undoing last swipe for user {UserId}", userId);
                return InteractionResult.Failed("Unable to undo last swipe. Please try again.");
            }
        }

        public async Task<OperationResult<List<UserLikeDTO>>> GetUsersWhoLikedMe(Guid userId, int page = 1, int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Getting users who liked user {UserId}, page {Page}", userId, page);

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 50) pageSize = 20;

                // Verify user exists
                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser == null)
                {
                    return OperationResult<List<UserLikeDTO>>.NotFound("User not found.");
                }

                // Get users who liked current user (but current user hasn't liked back)
                var usersWhoLikedMe = await _dbContext.UserLikes
                    .Where(ul => ul.LikedUserId == userId && ul.IsLike && !ul.IsMatch)
                    .Include(ul => ul.LikerUser)
                        .ThenInclude(u => u.Photos)
                    .Where(ul => ul.LikerUser.IsActive) // Only active users
                    .OrderByDescending(ul => ul.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new List<UserLikeDTO>();

                foreach (var like in usersWhoLikedMe)
                {
                    var liker = like.LikerUser;

                    // Calculate distance if both users have location
                    double? distance = null;
                    if (currentUser.Latitude.HasValue && currentUser.Longitude.HasValue &&
                        liker.Latitude.HasValue && liker.Longitude.HasValue)
                    {
                        distance = LocationHelper.CalculateDistance(
                            (double)currentUser.Latitude, (double)currentUser.Longitude,
                            (double)liker.Latitude, (double)liker.Longitude);
                    }

                    var userLikeDto = new UserLikeDTO
                    {
                        UserId = liker.Id,
                        FirstName = liker.FirstName,
                        LastName = liker.LastName,
                        Age = liker.DateOfBirth.HasValue ?
                            DateTime.UtcNow.Year - liker.DateOfBirth.Value.Year : 0,
                        PrimaryPhoto = liker.Photos
                            .FirstOrDefault(p => p.IsPrimary)?.Url ??
                            liker.Photos
                            .OrderBy(p => p.PhotoOrder)
                            .FirstOrDefault()?.Url,
                        Heritage = liker.Heritage,
                        LikeType = like.LikeType,
                        LikedAt = like.CreatedAt,
                        DistanceKm = distance
                    };

                    result.Add(userLikeDto);
                }

                _logger.LogInformation("Retrieved {Count} users who liked user {UserId}", result.Count, userId);
                return OperationResult<List<UserLikeDTO>>.Successful(result)
                    .WithMetadata("page", page)
                    .WithMetadata("page_size", pageSize)
                    .WithMetadata("has_more", result.Count == pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users who liked user {UserId}", userId);
                return OperationResult<List<UserLikeDTO>>.Failed("Unable to retrieve users who liked you. Please try again.");
            }
        }
    }
}