using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
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

        public async Task<LikeResult> ExpressInterest(Guid likerId, Guid likedId, LikeType interestType = LikeType.Regular)
        {
            try
            {
                _logger.LogInformation("User {LikerId} expressing {InterestType} interest in user {LikedId}",
                    likerId, interestType, likedId);

                // Check if user can like based on subscription limits
                var canLike = await _subscriptionService.CanUserLikeAsync(likerId, interestType);
                if (!canLike)
                {
                    var usageLimits = await _subscriptionService.GetUsageLimitsAsync(likerId);
                    var limitType = interestType == LikeType.SuperLike ? "super likes" : "likes";
                    return LikeResult.LimitReached($"You've reached your daily {limitType} limit.", usageLimits);
                }

                // Check if user is reported
                var isReported = await _reportingService.IsUserReportedAsync(likerId, likedId);
                if (isReported.Data)
                {
                    return LikeResult.UserBlocked();
                }

                // Check if already liked/passed
                var existingInteraction = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == likerId && ul.LikedUserId == likedId);

                if (existingInteraction != null)
                {
                    return LikeResult.UserAlreadyProcessed();
                }

                // Verify target user exists and is active
                var targetUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == likedId && u.IsActive);

                if (targetUser == null)
                {
                    return LikeResult.UserNotFound();
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
                await _subscriptionService.RecordLikeUsageAsync(likerId, interestType);

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
                var updatedLimits = await _subscriptionService.GetUsageLimitsAsync(likerId);

                return isMatch
                    ? LikeResult.MatchCreated(updatedLimits)
                    : LikeResult.LikeRecorded(updatedLimits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expressing interest from user {LikerId} to user {LikedId}", likerId, likedId);
                return (LikeResult)LikeResult.Failed("Unable to process your like. Please try again.");
            }
        }

        public async Task<OperationResult> DismissUser(Guid dismisserId, Guid dismissedId)
        {
            try
            {
                _logger.LogInformation("User {DismisserId} dismissing user {DismissedId}", dismisserId, dismissedId);

                // Check if already liked/passed
                var existingInteraction = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == dismisserId && ul.LikedUserId == dismissedId);

                if (existingInteraction != null)
                {
                    return OperationResult.BusinessRuleViolation("You have already interacted with this user.");
                }

                // Verify target user exists
                var targetUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == dismissedId && u.IsActive);

                if (targetUser == null)
                {
                    return OperationResult.NotFound("User not found or inactive.");
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
                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing user {DismissedId} by user {DismisserId}", dismissedId, dismisserId);
                return OperationResult.Failed("Unable to dismiss user. Please try again.");
            }
        }

        public async Task<OperationResult> UndoLastSwipe(Guid userId)
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
                    return OperationResult.BusinessRuleViolation("No recent swipe found to undo.");
                }

                // Check if it's been too long (only allow undo within 5 minutes)
                if (DateTime.UtcNow - lastSwipe.CreatedAt > TimeSpan.FromMinutes(5))
                {
                    return OperationResult.BusinessRuleViolation("Cannot undo a swipe older than 5 minutes.");
                }

                // Check if it resulted in a match - matches cannot be undone this way
                if (lastSwipe.IsMatch)
                {
                    return OperationResult.BusinessRuleViolation("Cannot undo a swipe that resulted in a match.");
                }

                // Remove the swipe record
                _dbContext.UserLikes.Remove(lastSwipe);
                await _dbContext.SaveChangesAsync();

                // Clear discovery cache to potentially show the user again
                await _cacheInvalidation.InvalidateUserDiscovery(userId);

                _logger.LogInformation("User {UserId} successfully undid last swipe on user {TargetUserId}",
                    userId, lastSwipe.LikedUserId);

                return OperationResult.Successful().WithMetadata("undone_user_id", lastSwipe.LikedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error undoing last swipe for user {UserId}", userId);
                return OperationResult.Failed("Unable to undo last swipe. Please try again.");
            }
        }
    }
}