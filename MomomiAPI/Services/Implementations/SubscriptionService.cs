using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(MomomiDbContext dbContext, ILogger<SubscriptionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<SubscriptionStatusDTO> GetUserSubscriptionAsync(Guid userId)
        {
            try
            {
                var subscription = await _dbContext.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (subscription == null)
                {
                    // Create default free subscription
                    subscription = new UserSubscription
                    {
                        UserId = userId,
                        SubscriptionType = SubscriptionType.Free,
                        StartsAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _dbContext.UserSubscriptions.Add(subscription);
                    await _dbContext.SaveChangesAsync();
                }

                // Check if premium subscription has expired
                if (subscription.SubscriptionType == SubscriptionType.Premium &&
                    subscription.ExpiresAt.HasValue &&
                    subscription.ExpiresAt < DateTime.UtcNow)
                {
                    subscription.SubscriptionType = SubscriptionType.Free;
                    subscription.IsActive = true;
                    subscription.ExpiresAt = null;
                    await _dbContext.SaveChangesAsync();
                }

                return new SubscriptionStatusDTO
                {
                    SubscriptionType = subscription.SubscriptionType,
                    IsActive = subscription.IsActive,
                    StartsAt = subscription.StartsAt,
                    ExpiresAt = subscription.ExpiresAt,
                    DaysRemaining = subscription.ExpiresAt.HasValue ?
                        Math.Max(0, (int)(subscription.ExpiresAt.Value - DateTime.UtcNow).TotalDays) : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription for user {UserId}", userId);
                return new SubscriptionStatusDTO
                {
                    SubscriptionType = SubscriptionType.Free,
                    IsActive = true
                };
            }
        }

        public async Task<bool> UpgradeToPremiumAsync(Guid userId, int durationMonths = 1)
        {
            try
            {
                var subscription = await _dbContext.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (subscription == null)
                {
                    subscription = new UserSubscription
                    {
                        UserId = userId,
                        SubscriptionType = SubscriptionType.Premium,
                        StartsAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMonths(durationMonths),
                        IsActive = true
                    };

                    _dbContext.UserSubscriptions.Add(subscription);
                }
                else
                {
                    subscription.SubscriptionType = SubscriptionType.Premium;
                    subscription.StartsAt = DateTime.UtcNow;
                    subscription.ExpiresAt = DateTime.UtcNow.AddMonths(durationMonths);
                    subscription.IsActive = true;
                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {UserId} upgraded to Premium for {Months} months", userId, durationMonths);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upgrading user {UserId} to premium", userId);
                return false;
            }
        }

        public async Task<bool> CancelSubscriptionAsync(Guid userId)
        {
            try
            {
                var subscription = await _dbContext.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (subscription != null)
                {
                    subscription.SubscriptionType = SubscriptionType.Free;
                    subscription.ExpiresAt = null;
                    subscription.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} cancelled premium subscription", userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling subscription for user {UserId}", userId);
                return false;
            }
        }

        public async Task<UsageLimitsDTO> GetUsageLimitsAsync(Guid userId)
        {
            try
            {
                var subscription = await GetUserSubscriptionAsync(userId);
                var usageLimit = await GetOrCreateUsageLimitAsync(userId);

                // Reset daily limits if needed
                if (usageLimit.LastResetDate.Date < DateTime.UtcNow.Date)
                {
                    await ResetDailyLimitsAsync(userId);
                    usageLimit = await GetOrCreateUsageLimitAsync(userId);
                }

                // Reset weekly limits if needed (assuming week starts on Monday)
                var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1);
                if (usageLimit.LastWeeklyReset.Date < weekStart)
                {
                    await ResetWeeklyLimitsAsync(userId);
                    usageLimit = await GetOrCreateUsageLimitAsync(userId);
                }

                return new UsageLimitsDTO
                {
                    SubscriptionType = subscription.SubscriptionType,

                    // Daily limits
                    LikesUsedToday = usageLimit.LikesUsedToday,
                    MaxLikesPerDay = GetMaxLikesPerDay(subscription.SubscriptionType),
                    BonusLikesFromAds = usageLimit.BonusLikesFromAds,

                    // Super likes
                    SuperLikesUsedToday = usageLimit.SuperLikesUsedToday,
                    MaxSuperLikesPerDay = GetMaxSuperLikesPerDay(subscription.SubscriptionType),
                    SuperLikesUsedThisWeek = usageLimit.SuperLikesUsedThisWeek,
                    MaxSuperLikesPerWeek = GetMaxSuperLikesPerWeek(subscription.SubscriptionType),

                    //Match limits - now unlimited for all users

                    MatchesCount = 0, // Always 0 since unlimited
                    MaxMatches = int.MaxValue, // Unlimited
                    RemainingMatches = int.MaxValue, // Unlimited

                    // Ads
                    AdsWatchedToday = usageLimit.AdsWatchedToday,
                    MaxAdsPerDay = GetMaxAdsPerDay(subscription.SubscriptionType),

                    // Calculated remaining
                    RemainingLikes = Math.Max(0, GetMaxLikesPerDay(subscription.SubscriptionType) - usageLimit.LikesUsedToday + usageLimit.BonusLikesFromAds),
                    RemainingSuperLikes = subscription.SubscriptionType == SubscriptionType.Premium ?
                        Math.Max(0, GetMaxSuperLikesPerDay(subscription.SubscriptionType) - usageLimit.SuperLikesUsedToday) :
                        Math.Max(0, GetMaxSuperLikesPerWeek(subscription.SubscriptionType) - usageLimit.SuperLikesUsedThisWeek),
                    RemainingAds = Math.Max(0, GetMaxAdsPerDay(subscription.SubscriptionType) - usageLimit.AdsWatchedToday)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage limits for user {UserId}", userId);
                return new UsageLimitsDTO { SubscriptionType = SubscriptionType.Free };
            }
        }

        public async Task<bool> CanUserLikeAsync(Guid userId, LikeType likeType = LikeType.Regular)
        {
            try
            {
                var usageLimits = await GetUsageLimitsAsync(userId);

                if (likeType == LikeType.Regular)
                {
                    return usageLimits.RemainingLikes > 0;
                }
                else // SuperLike
                {
                    return usageLimits.RemainingSuperLikes > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} can like", userId);
                return false;
            }
        }

        public bool CanUserMatchAsync(Guid userId)
        {
            return true; // Currently all users can match, no limits enforced
        }

        public async Task<bool> RecordLikeUsageAsync(Guid userId, LikeType likeType)
        {
            try
            {
                var usageLimit = await GetOrCreateUsageLimitAsync(userId);

                if (likeType == LikeType.Regular)
                {
                    if (usageLimit.BonusLikesFromAds > 0)
                    {
                        usageLimit.BonusLikesFromAds--;
                    }
                    else
                    {
                        usageLimit.LikesUsedToday++;
                    }
                }
                else // SuperLike
                {
                    usageLimit.SuperLikesUsedToday++;
                    usageLimit.SuperLikesUsedThisWeek++;
                }

                usageLimit.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Recorded {LikeType} usage for user {UserId}", likeType, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording like usage for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> RecordAdWatchedAsync(Guid userId)
        {
            try
            {
                var usageLimit = await GetOrCreateUsageLimitAsync(userId);
                var subscription = await GetUserSubscriptionAsync(userId);

                // Only free users can watch ads
                if (subscription.SubscriptionType != SubscriptionType.Free)
                    return false;

                // Check if user has reached daily ad limit
                if (usageLimit.AdsWatchedToday >= GetMaxAdsPerDay(SubscriptionType.Free))
                    return false;

                usageLimit.AdsWatchedToday++;
                usageLimit.BonusLikesFromAds++;
                usageLimit.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {UserId} watched ad and earned bonus like", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording ad watched for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetDailyLimitsAsync(Guid userId)
        {
            try
            {
                var usageLimit = await GetOrCreateUsageLimitAsync(userId);

                usageLimit.LikesUsedToday = 0;
                usageLimit.SuperLikesUsedToday = 0;
                usageLimit.AdsWatchedToday = 0;
                usageLimit.BonusLikesFromAds = 0;
                usageLimit.LastResetDate = DateTime.UtcNow.Date;
                usageLimit.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Reset daily limits for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting daily limits for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetWeeklyLimitsAsync(Guid userId)
        {
            try
            {
                var usageLimit = await GetOrCreateUsageLimitAsync(userId);

                usageLimit.SuperLikesUsedThisWeek = 0;
                usageLimit.LastWeeklyReset = DateTime.UtcNow.Date;
                usageLimit.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Reset weekly limits for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting weekly limits for user {UserId}", userId);
                return false;
            }
        }

        private async Task<UserUsageLimit> GetOrCreateUsageLimitAsync(Guid userId)
        {
            var usageLimit = await _dbContext.UserUsageLimits
                .FirstOrDefaultAsync(ul => ul.UserId == userId);

            if (usageLimit == null)
            {
                usageLimit = new UserUsageLimit
                {
                    UserId = userId,
                    LastResetDate = DateTime.UtcNow.Date,
                    LastWeeklyReset = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1)
                };

                _dbContext.UserUsageLimits.Add(usageLimit);
                await _dbContext.SaveChangesAsync();
            }

            return usageLimit;
        }

        // Subscription limits configuration
        private static int GetMaxLikesPerDay(SubscriptionType subscriptionType) =>
            subscriptionType == SubscriptionType.Premium ? int.MaxValue : 10;

        private static int GetMaxSuperLikesPerDay(SubscriptionType subscriptionType) =>
            subscriptionType == SubscriptionType.Premium ? 10 : 0;

        private static int GetMaxSuperLikesPerWeek(SubscriptionType subscriptionType) =>
            subscriptionType == SubscriptionType.Premium ? 70 : 1; // 10 per day * 7 days vs 1 per week

        private static int GetMaxAdsPerDay(SubscriptionType subscriptionType) =>
            subscriptionType == SubscriptionType.Free ? 10 : 0;
    }
}
