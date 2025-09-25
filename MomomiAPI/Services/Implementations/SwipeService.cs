using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class SwipeService : ISwipeService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<SwipeService> _logger;
        private readonly IPushNotificationService _pushNotificationService;

        private static readonly SwipeType[] PositiveSwipeTypes = { SwipeType.Like, SwipeType.SuperLike };

        public SwipeService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<SwipeService> logger,
            IPushNotificationService pushNotificationService)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
            _pushNotificationService = pushNotificationService;
        }

        public async Task<SwipeResult> Swipe(Guid userId, Guid targetUserId, SwipeType swipeType)
        {
            if (swipeType == SwipeType.Pass)
            {
                return await PassUser(userId, targetUserId);
            }

            var swipeUserData = await GetUserDataForSwipe(userId, targetUserId, swipeType);
            if (!swipeUserData.IsValid)
            {
                return swipeUserData.Error!;
            }

            return swipeType switch
            {
                SwipeType.Like => await LikeUser(swipeUserData),
                SwipeType.SuperLike => await SuperLikeUser(swipeUserData),
                _ => SwipeResult.Error("Invalid swipe type", targetUserId)
            };
        }

        public async Task<SwipeResult> RewardedSwipe(Guid userId, Guid targetUserId, SwipeType swipeType)
        {
            var swipeUserData = await GetUserDataForSwipe(userId, targetUserId, swipeType, true);
            if (!swipeUserData.IsValid)
            {
                return swipeUserData.Error!;
            }

            // For rewarded swipes, we only allow normal like
            return await LikeUser(swipeUserData, true);
        }

        public async Task<SwipeResult> LikeUser(SwipeUserData swipeUserData, bool isRewarded = false)
        {
            var currentUser = swipeUserData.CurrentUser;
            var targetUser = swipeUserData.TargetUser;
            var isAlreadyLikedByTarget = swipeUserData.IsAlreadyLikedByTarget;
            var isSuperLikedByTarget = swipeUserData.IsSuperLikedByTarget;

            try
            {
                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        ProcessNewSwipeRecord(currentUser.Id, targetUser.Id, SwipeType.Like, currentUser.UsageLimit!, isRewarded);
                        ProcessLikeMatch(currentUser, targetUser, swipeUserData, false);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        FireAndForgetHelper.Run(
                           SendNotificationIfNeeded(currentUser, targetUser, isAlreadyLikedByTarget, false),
                           _logger,
                           "Send super like or matched notification");

                        return isAlreadyLikedByTarget ? SwipeResult.MatchCreated(targetUser.Id) : SwipeResult.LikeRecorded(targetUser.Id);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking between user {UserId} and {TargetUserId}", currentUser.Id, targetUser.Id);
                return SwipeResult.Error("An error occurred while processing your like", targetUser.Id);
            }
        }

        public async Task<SwipeResult> SuperLikeUser(SwipeUserData swipeUserData)
        {
            var currentUser = swipeUserData.CurrentUser;
            var targetUser = swipeUserData.TargetUser;
            var isAlreadyLikedByTarget = swipeUserData.IsAlreadyLikedByTarget;
            var isSuperLikedByTarget = swipeUserData.IsSuperLikedByTarget;

            try
            {
                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        ProcessNewSwipeRecord(currentUser.Id, targetUser.Id, SwipeType.SuperLike, currentUser.UsageLimit);

                        ProcessLikeMatch(currentUser, targetUser, swipeUserData, true);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        FireAndForgetHelper.Run(
                            SendNotificationIfNeeded(currentUser, targetUser, isSuperLikedByTarget, true),
                            _logger,
                            "Send super like or matched notification");


                        return isAlreadyLikedByTarget ? SwipeResult.MatchCreated(targetUser.Id) : SwipeResult.SuperLikeRecorded(targetUser.Id);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error super liking between user {UserId} and {TargetUserId}", currentUser.Id, targetUser.Id);
                return SwipeResult.Error("An error occurred while processing your super like", targetUser.Id);
            }
        }

        public async Task<SwipeResult> PassUser(Guid userId, Guid dismissedUserId)
        {
            try
            {
                var validationData = await ValidatePassUser(userId, dismissedUserId);
                if (!validationData.IsValid)
                {
                    return validationData.Error!;
                }

                ProcessNewSwipeRecord(userId, dismissedUserId, SwipeType.Pass);

                await _dbContext.SaveChangesAsync();

                return SwipeResult.PassRecorded(dismissedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pass from user {UserId} to user {DismissedUserId}",
                    userId, dismissedUserId);
                return SwipeResult.Error("An error occurred while processing your pass", dismissedUserId);
            }
        }

        public async Task<SwipeResult> UndoSwipe(Guid userId)
        {
            try
            {
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-2);

                var lastPassSwipe = await _dbContext.UserSwipes
                    .Where(us => us.SwiperUserId == userId
                        && us.SwipeType == SwipeType.Pass
                        && us.CreatedAt >= fiveMinutesAgo)
                    .OrderByDescending(us => us.CreatedAt)
                    .FirstOrDefaultAsync();

                if (lastPassSwipe == null)
                {
                    return SwipeResult.NoRecentPassToUndo();
                }

                _dbContext.UserSwipes.Remove(lastPassSwipe);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {UserId} undid last pass swipe for user {SwipedUserId}",
                    userId, lastPassSwipe.SwipedUserId);

                return SwipeResult.SwipeUndone(lastPassSwipe.SwipedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error undoing last swipe for user {UserId}", userId);
                return SwipeResult.Error("An error occurred while undoing your last swipe");
            }
        }

        // Single optimized query for all validation
        private async Task<SwipeUserData> GetUserDataForSwipe(Guid userId, Guid targetUserId, SwipeType swipeType, bool isRewarded = false)
        {
            // Single query that gets all needed data
            var currentUser = await _dbContext.Users
                .Include(u => u.UsageLimit)
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return SwipeUserData.Invalid(SwipeResult.UserNotFound(userId));
            }

            if (HasLimitReached(currentUser, swipeType, isRewarded))
            {
                return SwipeUserData.Invalid(SwipeResult.LimitReached("Like limit reached. Try again tomorrow", targetUserId));
            }

            // Batch query for all validations
            var validationData = await _dbContext.Users
                .Where(u => u.Id == targetUserId && u.IsActive)
                .Select(u => new
                {
                    TargetUser = u,
                    IsReported = _dbContext.UserReports
                        .Any(ur => ur.ReportedEmail == currentUser.Email && ur.ReporterEmail == u.Email),
                    ExistingTargetSwipeType = _dbContext.UserSwipes
                        .Where(us => us.SwiperUserId == targetUserId
                            && us.SwipedUserId == userId
                            && PositiveSwipeTypes.Contains(us.SwipeType)).Select(us => us.SwipeType).FirstOrDefault(),
                    ExistingUserSwipe = _dbContext.UserSwipes
                        .Any(us => us.SwiperUserId == userId && us.SwipedUserId == targetUserId),
                })
                .FirstOrDefaultAsync();

            if (validationData?.TargetUser == null)
            {
                return SwipeUserData.Invalid(SwipeResult.UserNotFound(targetUserId));
            }

            if (validationData.IsReported)
            {
                return SwipeUserData.Invalid(SwipeResult.UserBlocked(targetUserId));
            }

            if (validationData.ExistingUserSwipe)
            {
                return SwipeUserData.Invalid(SwipeResult.UserAlreadyProcessed(targetUserId));
            }

            return SwipeUserData.Valid(currentUser, validationData.TargetUser, Convert.ToBoolean(validationData.ExistingTargetSwipeType), validationData.ExistingTargetSwipeType == SwipeType.SuperLike);
        }

        private async Task<PassUserValidation> ValidatePassUser(Guid userId, Guid targetUserId)
        {
            var validationData = await _dbContext.Users
                .Where(u => u.Id == targetUserId && u.IsActive)
                .Select(u => new
                {
                    TargetExists = true,
                    ExistingSwipe = _dbContext.UserSwipes
                        .Any(us => us.SwiperUserId == userId && us.SwipedUserId == targetUserId)
                })
                .FirstOrDefaultAsync();

            if (validationData == null)
            {
                return PassUserValidation.Invalid(SwipeResult.UserNotFound(targetUserId));
            }

            if (validationData.ExistingSwipe)
            {
                return PassUserValidation.Invalid(SwipeResult.UserAlreadyProcessed(targetUserId));
            }

            return PassUserValidation.Valid();
        }

        private static bool HasLimitReached(User user, SwipeType swipeType, bool isRewarded = false)
        {
            if (user.Subscription == null || user.UsageLimit == null)
            {
                return true; // No subscription or usage limit record means no swipes allowed
            }

            ResetDailyLimitsIfNeeded(user.UsageLimit);

            var subscriptionType = user.Subscription.SubscriptionType;
            var usageLimit = user.UsageLimit;

            if (isRewarded)
            {
                var dailyLimit = AppConstants.Limits.FreeUserDailyAds;
                var usedToday = usageLimit.AdsWatchedToday;
                return usedToday >= dailyLimit;
            }
            else
            {

                var (dailyLimit, usedToday) = (subscriptionType, swipeType) switch
                {
                    (SubscriptionType.Free, SwipeType.Like) =>
                        (AppConstants.Limits.FreeUserDailyLikes, usageLimit.LikesUsedToday),
                    (SubscriptionType.Free, SwipeType.SuperLike) =>
                        (AppConstants.Limits.FreeUserDailySuperLikes, usageLimit.SuperLikesUsedToday),
                    (SubscriptionType.Premium, SwipeType.Like) =>
                        (AppConstants.Limits.PremiumUserDailyLikes, usageLimit.LikesUsedToday),
                    (SubscriptionType.Premium, SwipeType.SuperLike) =>
                        (AppConstants.Limits.PremiumUserDailySuperLikes, usageLimit.SuperLikesUsedToday),
                    _ => (0, int.MaxValue)
                };
                return usedToday >= dailyLimit;
            }
        }

        private static void ResetDailyLimitsIfNeeded(UserUsageLimit usageLimit)
        {
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);

            if (usageLimit.LastResetDate.Date < twentyFourHoursAgo)
            {
                usageLimit.LikesUsedToday = 0;
                usageLimit.SuperLikesUsedToday = 0;
                usageLimit.AdsWatchedToday = 0;
                usageLimit.LastResetDate = DateTime.UtcNow.Date;
            }
        }

        private void ProcessNewSwipeRecord(Guid currentUserId, Guid targetUserId, SwipeType swipeType, UserUsageLimit usageLimit = null, bool isRewarded = false)
        {
            var swipeRecord = CreateSwipeRecord(currentUserId, targetUserId, swipeType);
            _dbContext.UserSwipes.Add(swipeRecord);

            if (usageLimit != null)
            {
                // Update usage counter
                if (isRewarded)
                {
                    usageLimit!.AdsWatchedToday++;
                }
                else if (swipeType == SwipeType.Like)
                {
                    usageLimit!.LikesUsedToday++;
                }
                else if (swipeType == SwipeType.SuperLike)
                {
                    usageLimit!.SuperLikesUsedToday++;
                }
                usageLimit!.UpdatedAt = DateTime.UtcNow;
            }
        }

        private void ProcessLikeMatch(User currentUser, User targetUser, SwipeUserData swipeData, bool isSuperLikedByUser = false)
        {
            // Check fo match
            if (swipeData.IsAlreadyLikedByTarget)
            {
                var convSwipeType = (swipeData.IsSuperLikedByTarget || isSuperLikedByUser) ? SwipeType.SuperLike : SwipeType.Like;
                var conversationRecord = CreateConversationRecord(currentUser.Id, targetUser.Id, convSwipeType);
                _dbContext.Conversations.Add(conversationRecord);

                // Don't wait for cache invalidation - fire and forget
                FireAndForgetHelper.Run(
                    InvalidateRelevantCaches(currentUser.Id, targetUser.Id, swipeData.IsAlreadyLikedByTarget),
                    _logger,
                    "Invalidate caches after like match");
            }
        }

        private async Task SendNotificationIfNeeded(User currentUser, User targetUser, bool isAlreadyLikedByTarget, bool IsSuperLikedByUser)
        {
            // Send push notifications if user is not online
            var isTargetUserOnline = await _cacheService.GetAsync<bool>(CacheKeys.Users.OnlineStatus(targetUser.Id));
            var canSendNotification = !isTargetUserOnline && targetUser.NotificationsEnabled && !string.IsNullOrEmpty(targetUser.PushToken);

            if (isAlreadyLikedByTarget && canSendNotification)
            {
                await _pushNotificationService.SendNewMatchNotificationAsync(targetUser.PushToken!, currentUser.FirstName ?? "Someone");
            }
            else if (canSendNotification)
            {
                await _pushNotificationService.SendLikeNotificationAsync(targetUser.PushToken!, currentUser.FirstName ?? "Someone");
            }
        }

        private async Task InvalidateRelevantCaches(Guid userId, Guid targetUserId, bool isMatch = false)
        {
            var keysToRemove = new List<string>
            {
                CacheKeys.Users.Profile(userId)
            };

            if (isMatch)
            {
                keysToRemove.AddRange([
                    CacheKeys.Matching.UserMatches(userId),
                    CacheKeys.Matching.UserMatches(targetUserId),
                    CacheKeys.Messaging.UserConversations(userId),
                    CacheKeys.Messaging.UserConversations(targetUserId)
                ]);
            }

            await _cacheService.RemoveManyAsync(keysToRemove);

            _logger.LogDebug("Invalidated caches after swiping for {UserId}", userId);
        }

        private static UserSwipe CreateSwipeRecord(Guid userId, Guid targetUserId, SwipeType swipeType)
        {
            return new UserSwipe
            {
                SwiperUserId = userId,
                SwipedUserId = targetUserId,
                SwipeType = swipeType
            };
        }

        private static Conversation CreateConversationRecord(Guid userId, Guid targetUserId, SwipeType swipeType)
        {
            // ✅ Ensure consistent ordering for conversation participants
            var (user1Id, user2Id) = userId.CompareTo(targetUserId) < 0
                ? (userId, targetUserId)
                : (targetUserId, userId);

            return new Conversation
            {
                User1Id = user1Id,
                User2Id = user2Id,
                SwipeType = swipeType
            };
        }
    }
}