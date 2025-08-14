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

        private static readonly SwipeType[] PositiveSwipeTypes = { SwipeType.Like, SwipeType.SuperLike };

        public SwipeService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<SwipeService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<SwipeResult> LikeUser(Guid userId, Guid likedUserId)
        {
            try
            {
                // Single query to get all user data and validate in one go
                var userData = await GetUserDataForSwipe(userId, likedUserId);
                if (!userData.IsValid)
                {
                    return userData.Error!;
                }

                var currentUser = userData.CurrentUser!;
                var targetUser = userData.TargetUser!;

                // Reset usage limits if needed
                ResetDailyLimitsIfNeeded(currentUser.UsageLimit!);

                if (HasReachedUsageLimit(currentUser.UsageLimit!, SwipeType.Like, currentUser.Subscription!.SubscriptionType))
                {
                    return SwipeResult.LimitReached("Like limit reached. Try again tomorrow");
                }

                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {

                    // Use transaction for atomicity
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Create swipe record
                        var swipeRecord = CreateSwipeRecord(userId, likedUserId, SwipeType.Like);
                        _dbContext.UserSwipes.Add(swipeRecord);

                        // Update usage counter
                        currentUser.UsageLimit!.LikesUsedToday++;
                        currentUser.UsageLimit!.UpdatedAt = DateTime.UtcNow;

                        // Check fo match
                        var isMatch = userData.IsAlreadyLikedByTarget;
                        if (isMatch)
                        {
                            var convSwipeType = userData.IsSuperLikedByTarget ? SwipeType.SuperLike : SwipeType.Like;
                            var conversationRecord = CreateConversationRecord(userId, likedUserId, convSwipeType);
                            _dbContext.Conversations.Add(conversationRecord);
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Don't wait for cache invalidation - fire and forget
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await InvalidateRelevantCaches(userId, likedUserId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Cache invalidation failed for users {UserId}, {TargetUserId}", userId, likedUserId);
                            }
                        });

                        _logger.LogInformation("User {UserId} liked user {LikedUserId}. Match: {IsMatch}",
                            userId, likedUserId, isMatch);

                        return isMatch ? SwipeResult.MatchCreated() : SwipeResult.LikeRecorded();

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
                _logger.LogError(ex, "Error liking between user {UserId} and {TargetUserId}", userId, likedUserId);
                return SwipeResult.Error("An error occurred while processing your like");
            }
        }

        public async Task<SwipeResult> SuperLikeUser(Guid userId, Guid likedUserId)
        {
            try
            {
                // Single query to get all user data and validate in one go
                var userData = await GetUserDataForSwipe(userId, likedUserId);
                if (!userData.IsValid)
                {
                    return userData.Error!;
                }

                var currentUser = userData.CurrentUser!;
                var targetUser = userData.TargetUser!;

                // Reset usage limits if needed
                ResetDailyLimitsIfNeeded(currentUser.UsageLimit!);

                if (HasReachedUsageLimit(currentUser.UsageLimit!, SwipeType.SuperLike, currentUser.Subscription!.SubscriptionType))
                {
                    return SwipeResult.SuperLikeLimitReached("Super Like limit reached. Try again tomorrow");
                }

                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
                return await executionStrategy.ExecuteAsync(async () =>
                {

                    // Use transaction for atomicity
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Create swipe record
                        var swipeRecord = CreateSwipeRecord(userId, likedUserId, SwipeType.SuperLike);
                        _dbContext.UserSwipes.Add(swipeRecord);

                        // Update usage counter
                        currentUser.UsageLimit!.SuperLikesUsedToday++;
                        currentUser.UsageLimit!.UpdatedAt = DateTime.UtcNow;

                        // Check fo match
                        var isMatch = userData.IsAlreadyLikedByTarget;
                        if (isMatch)
                        {
                            var conversationRecord = CreateConversationRecord(userId, likedUserId, SwipeType.SuperLike);
                            _dbContext.Conversations.Add(conversationRecord);
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Don't wait for cache invalidation - fire and forget
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await InvalidateRelevantCaches(userId, likedUserId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Cache invalidation failed for users {UserId}, {TargetUserId}", userId, likedUserId);
                            }
                        });

                        _logger.LogInformation("User {UserId} super liked user {LikedUserId}. Match: {IsMatch}",
                            userId, likedUserId, isMatch);

                        return isMatch ? SwipeResult.MatchCreated() : SwipeResult.SuperLikeRecorded();

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
                _logger.LogError(ex, "Error super liking between user {UserId} and {TargetUserId}", userId, likedUserId);
                return SwipeResult.Error("An error occurred while processing your super like");
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

                // Single atomic operation (no transaction needed)
                var swipeRecord = CreateSwipeRecord(userId, dismissedUserId, SwipeType.Pass);
                _dbContext.UserSwipes.Add(swipeRecord);
                await _dbContext.SaveChangesAsync();

                // Fire-and-forget cache invalidation for discovery
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RemoveUserFromDiscoveryCache(userId, dismissedUserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Discovery cache invalidation failed for user {UserId} during undo", userId);
                    }
                });

                _logger.LogInformation("User {UserId} passed user {DismissedUserId}", userId, dismissedUserId);

                return SwipeResult.PassRecorded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pass from user {UserId} to user {DismissedUserId}",
                    userId, dismissedUserId);
                return SwipeResult.Error("An error occurred while processing your pass");
            }
        }

        public async Task<SwipeResult> UndoLastSwipe(Guid userId)
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

                // Fire-and-forget cache invalidation for discovery
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InvalidateDiscoveryCaches(userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Discovery cache invalidation failed for user {UserId} during undo", userId);
                    }
                });

                _logger.LogInformation("User {UserId} undid last pass swipe for user {SwipedUserId}",
                    userId, lastPassSwipe.SwipedUserId);

                return SwipeResult.SwipeUndone();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error undoing last swipe for user {UserId}", userId);
                return SwipeResult.Error("An error occurred while undoing your last swipe");
            }
        }

        // Single optimized query for all validation
        private async Task<SwipeUserData> GetUserDataForSwipe(Guid userId, Guid targetUserId)
        {
            // Single query that gets all needed data
            var currentUser = await _dbContext.Users
                .Include(u => u.UsageLimit)
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return SwipeUserData.Invalid(SwipeResult.UserNotFound());
            }

            // Batch query for all validations
            var validationData = await _dbContext.Users
                .Where(u => u.Id == targetUserId && u.IsActive)
                .Select(u => new
                {
                    TargetUser = u,
                    IsReported = _dbContext.UserReports
                        .Any(ur => ur.ReportedId == userId && ur.ReporterId == targetUserId),
                    ExistingSwipe = _dbContext.UserSwipes
                        .Any(us => us.SwiperUserId == userId && us.SwipedUserId == targetUserId),
                    IsAlreadyLikedByTarget = _dbContext.UserSwipes
                        .Any(us => us.SwiperUserId == targetUserId
                            && us.SwipedUserId == userId
                            && PositiveSwipeTypes.Contains(us.SwipeType)),
                    ExistingSwipeType = _dbContext.UserSwipes
                        .Where(us => us.SwiperUserId == userId && us.SwipedUserId == targetUserId)
                        .Select(us => us.SwipeType)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            if (validationData?.TargetUser == null)
            {
                return SwipeUserData.Invalid(SwipeResult.UserNotFound());
            }

            if (validationData.IsReported)
            {
                return SwipeUserData.Invalid(SwipeResult.UserBlocked());
            }

            if (validationData.ExistingSwipe)
            {
                return SwipeUserData.Invalid(SwipeResult.UserAlreadyProcessed());
            }

            return SwipeUserData.Valid(currentUser, validationData.TargetUser, validationData.IsAlreadyLikedByTarget, validationData.ExistingSwipeType == SwipeType.SuperLike);
        }

        // Removes a specific user from all discovery cache entries for the given user
        // Used when user swipes on someone to prevent them from appearing again
        // More efficient than invalidting entire discovery cache
        public async Task RemoveUserFromDiscoveryCache(Guid userId, Guid removedUserId)
        {
            var keys = new List<string>();
            keys.Add(CacheKeys.Discovery.GlobalResults(userId));
            keys.Add(CacheKeys.Discovery.LocalResults(userId));

            // Get all discovery caches in one batch operation
            var cachedResults = await _cacheService.GetManyAsync<CachedDiscoveryData>(keys);

            var updatedCaches = new Dictionary<string, CachedDiscoveryData>();
            var keysToRemove = new List<string>();

            foreach (var (cacheKey, cachedData) in cachedResults)
            {
                if (cachedData?.Users != null)
                {
                    // Remove the swiped user from the cached results
                    var originalCount = cachedData.Users.Count;
                    cachedData.Users.RemoveAll(u => u.Id == removedUserId);

                    if (cachedData.Users.Count != originalCount)
                    {
                        // Only update if we actually removed someone
                        cachedData.ActualCount = cachedData.Users.Count;
                        updatedCaches[cacheKey] = cachedData;

                        _logger.LogDebug("Removed user {RemovedUserId} from discovery cache {CacheKey} for user {UserId}",
                            removedUserId, cacheKey, userId);
                    }
                }
            }

            // Batch update all modified caches
            if (updatedCaches.Any())
            {
                var expiries = updatedCaches.Keys.ToDictionary(
                    key => key,
                    _ => CacheKeys.Duration.DiscoveryResults
                );

                await _cacheService.SetManyAsync(updatedCaches, expiries);
                _logger.LogDebug("Updated {Count} discovery caches after removing user {RemovedUserId} for user {UserId}",
                    updatedCaches.Count, removedUserId, userId);
            }
        }

        // Usage limit logic 
        private static bool HasReachedUsageLimit(UserUsageLimit usageLimit, SwipeType swipeType, SubscriptionType subscriptionType)
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

        // Reset daily limits if needed
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


        // Cache Invalidation for related data
        private async Task InvalidateRelevantCaches(Guid userId, Guid targetUserId, bool isMatch = false)
        {
            // For the target user: only invalidate matches if it was a positive swipe
            var keysToRemove = new List<string>
            {
                CacheKeys.Matching.UserMatches(userId)
            };

            if (isMatch)
            {
                keysToRemove.Add(CacheKeys.Matching.UserMatches(targetUserId));
            }

            var removeMatchesTask = _cacheService.RemoveManyAsync(keysToRemove);

            await Task.WhenAll(removeMatchesTask, RemoveUserFromDiscoveryCache(userId, targetUserId));

            _logger.LogDebug("Invalidated caches after swiping for {UserId}", userId);
        }

        private async Task InvalidateDiscoveryCaches(Guid userId)
        {
            var keysToRemove = new[]
            {
               CacheKeys.Discovery.GlobalResults(userId),
               CacheKeys.Discovery.LocalResults(userId),
            };

            await _cacheService.RemoveManyAsync(keysToRemove);
            _logger.LogDebug("Invalidated caches after swiping for {UserId}", userId);
        }

        // Validation for PassUser - single query
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
                return PassUserValidation.Invalid(SwipeResult.UserNotFound());
            }

            if (validationData.ExistingSwipe)
            {
                return PassUserValidation.Invalid(SwipeResult.UserAlreadyProcessed());
            }

            return PassUserValidation.Valid();
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