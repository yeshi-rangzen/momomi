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
    public class DiscoveryService : IDiscoveryService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<DiscoveryService> _logger;

        public DiscoveryService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<DiscoveryService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<DiscoveryResult> DiscoverCandidatesAsync(Guid userId, int maxResults = 30)
        {
            var currentUser = await _dbContext.Users
                .Include(u => u.Preferences)
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (currentUser == null)
            {
                return DiscoveryResult.UserNotFound();
            }

            var discoveryMode = currentUser.EnableGlobalDiscovery ? "global" : "local";
            var cacheKey = CacheKeys.Discovery.Results(userId, discoveryMode);

            var cachedResult = await _cacheService.GetOrSetAsync(
            cacheKey,
            async () => await PerformDiscoveryAndCreateCacheData(currentUser, maxResults, discoveryMode),
            CacheKeys.Duration.DiscoveryResults
            );

            // Check if cached result is still valid (location/time validation)
            if (cachedResult != null && !IsCacheValid(cachedResult, currentUser))
            {
                _logger.LogDebug("Cached data invalid for user {UserId}, invalidating cache", userId);

                // Remove invalid cache and get fresh data
                await _cacheService.RemoveAsync(cacheKey);

                // Get fresh data )this will hit database since cache is now empty)
                cachedResult = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await PerformDiscoveryAndCreateCacheData(currentUser, maxResults, discoveryMode),
                    CacheKeys.Duration.DiscoveryResults);
            }

            if (cachedResult == null)
            {
                _logger.LogWarning("Failed to get discovery data for user {UserId}", userId);
                return DiscoveryResult.NoUsersFound(discoveryMode, maxResults);
            }

            var fromCache = cachedResult.CachedAt < DateTime.UtcNow.AddMinutes(-1); // Consider "from cache" if older than 1 minute

            _logger.LogDebug("Serving discovery results for user {UserId}, fromCache: {FromCache}", userId, fromCache);

            return DiscoveryResult.Success(
                    cachedResult.Users,
                    discoveryMode,
                    maxResults,
                    fromCache: fromCache
                );
        }

        private async Task<CachedDiscoveryData> PerformDiscoveryAndCreateCacheData(User currentUser, int maxResults, string discoveryMode)
        {
            List<User> discoveredUsers;
            if (discoveryMode == "global")
            {
                discoveredUsers = await DiscoverGlobalCandidatesAsync(currentUser, maxResults);
            }
            else
            {
                discoveredUsers = await DiscoverLocalCandidatesAsync(currentUser, maxResults);
            }

            var discoveredUserDtos = discoveredUsers.Select(user => UserMapper.UserToDiscoveryDTO(user)).ToList();

            return new CachedDiscoveryData
            {
                Users = discoveredUserDtos,
                DiscoveryMode = discoveryMode,
                RequestedCount = maxResults,
                ActualCount = discoveredUserDtos.Count,
                FromCache = false,
                CachedAt = DateTime.UtcNow,
                UserLatitude = currentUser.Latitude,
                UserLongitude = currentUser.Longitude,
                HasMore = maxResults == discoveredUserDtos.Count
            };
        }

        private async Task<List<User>> DiscoverLocalCandidatesAsync(User currentUser, int maxResults)
        {
            // For local discovery, we need to handle distance filtering differently
            // due to the complexity of SQL-based distance calculations

            var query = BuildBaseCandidateQuery(currentUser).Where(u => u.Latitude != 0 && u.Longitude != 0);

            var candidateBatch = await query
                .OrderBy(u => u.LastActive) // Add some ordering for consistency
                .Take(200)
                .ToListAsync();

            // Apply distance filtering in memory (unavoidable for complex distance calculations)
            var localCandidates = candidateBatch
                .Where(u => LocationHelper.IsWithinDistance(
                    currentUser.Latitude, currentUser.Longitude,
                    u.Latitude, u.Longitude,
                    currentUser.MaxDistanceKm))
                .ToList();

            // Apply preference filters
            var filteredCandidates = ApplyPreferenceFilters(localCandidates, currentUser);

            // Apply premium filters if user is a subscriber
            if (IsActiveSubscriber(currentUser))
            {
                filteredCandidates = ApplyPremiumFilters(filteredCandidates, currentUser);
            }

            var finalResults = filteredCandidates.Take(maxResults).ToList();

            _logger.LogInformation("Local discovery for user {UserId}: Found {Count}/{BatchSize} users within {Distance}km after all filtering",
                currentUser.Id, finalResults.Count, candidateBatch.Count, currentUser.MaxDistanceKm);

            return finalResults;
        }

        private async Task<List<User>> DiscoverGlobalCandidatesAsync(User currentUser, int maxResults)
        {
            var candidates = await BuildBaseCandidateQuery(currentUser)
                .OrderByDescending(u => u.LastActive) // Prioritize recently active users
                .Take(maxResults * 3) // Get 3x more to account for preference filtering
                .ToListAsync();

            // Apply preference filters in memory
            var filteredCandidates = ApplyPreferenceFilters(candidates, currentUser);

            // Apply premium filters if user is a subscriber
            if (IsActiveSubscriber(currentUser))
            {
                filteredCandidates = ApplyPremiumFilters(filteredCandidates, currentUser);
            }

            var finalResults = filteredCandidates.Take(maxResults).ToList();

            _logger.LogInformation("Global discovery for user {UserId}: Found {Count} users after filtering from {CandidateCount} candidates",
                currentUser.Id, finalResults.Count, candidates.Count);

            return finalResults;
        }

        private IQueryable<User> BuildBaseCandidateQuery(User currentUser)
        {
            var swipedUserIdsQuery = _dbContext.UserSwipes
               .Where(ul => ul.SwiperUserId == currentUser.Id)
               .Select(ul => ul.SwipedUserId);

            var reportExclusionQuery = _dbContext.UserReports
                .Where(ur => ur.ReporterId == currentUser.Id || ur.ReportedId == currentUser.Id)
                .Select(ur => ur.ReporterId == currentUser.Id ? ur.ReportedId : ur.ReporterId);

            var query = _dbContext.Users
                .Include(u => u.Preferences)
                .Include(u => u.Photos)
                .Where(u => u.IsActive
                    && u.IsDiscoverable
                    && u.Id != currentUser.Id
                    && u.InterestedIn == currentUser.Gender
                    && !swipedUserIdsQuery.Contains(u.Id)
                    && !reportExclusionQuery.Contains(u.Id)
                    && u.IsGloballyDiscoverable == currentUser.EnableGlobalDiscovery // Global constraint
                    );

            // Apply core filters (available to all users)
            query = ApplyCoreMatchingFilters(query, currentUser);

            return query;
        }

        #region Private Helper Methods
        // Validates if cached discovery data is still valid
        // Checks location changes for local discovery
        private bool IsCacheValid(CachedDiscoveryData cache, User currentUser)
        {
            // Check if cache is too old (additional safety check)
            if (cache.CachedAt < DateTime.UtcNow.Subtract(CacheKeys.Duration.DiscoveryResults))
            {
                _logger.LogDebug("Cache expired for user {UserId}", currentUser.Id);
                return false;
            }

            // Check if location changed significantly for local discovery
            if (cache.DiscoveryMode == "local" && cache.UserLatitude.HasValue && cache.UserLongitude.HasValue)
            {
                var distance = LocationHelper.CalculateDistance(
                    currentUser.Latitude, currentUser.Longitude, cache.UserLatitude.Value, cache.UserLongitude.Value);

                if (distance > 5)
                {
                    _logger.LogDebug("Location changed significantly ({Distance}km) for user {UserId}, invalidating cache", distance, currentUser.Id);
                    return false;
                }
            }

            // Check if cache is empty but hasMore
            if (cache.Users.Count == 0 && cache.HasMore)
            {
                return false;
            }

            return true;
        }

        private static IQueryable<User> ApplyCoreMatchingFilters(IQueryable<User> query, User currentUser)
        {
            // Gender preference filter
            query = query.Where(u => u.Gender == currentUser.InterestedIn);

            // Age filters
            var today = DateTime.UtcNow.Date;
            var minBirthDate = today.AddYears(-currentUser.MaxAge);
            var maxBirthDate = today.AddYears(-currentUser.MinAge);

            query = query.Where(u => u.DateOfBirth >= minBirthDate && u.DateOfBirth <= maxBirthDate);

            return query;
        }

        private static List<User> ApplyPreferenceFilters(List<User> users, User currentUser)
        {
            var preferredHeritage = currentUser.Preferences?.PreferredHeritage?.ToList();
            var preferredReligions = currentUser.Preferences?.PreferredReligions?.ToList();
            var preferredLanguages = currentUser.Preferences?.LanguagePreference?.ToList();

            return users.Where(u =>
            {
                // Heritage compatibility filter
                if (preferredHeritage is { Count: > 0 })
                {
                    if (u.Heritage.Count > 0 && !u.Heritage.Any(h => preferredHeritage.Contains(h)))
                        return false;
                }

                // Religion compatibility filter
                if (preferredReligions is { Count: > 0 })
                {
                    if (u.Religion.Count > 0 && !u.Religion.Any(r => preferredReligions.Contains(r)))
                        return false;
                }

                // Language compatibility filter
                if (preferredLanguages is { Count: > 0 })
                {
                    if (u.LanguagesSpoken.Count > 0 && !u.LanguagesSpoken.Any(l => preferredLanguages.Contains(l)))
                        return false;
                }

                return true;

            }).ToList();
        }

        private static bool IsActiveSubscriber(User currentUser)
        {
            return currentUser.Subscription?.SubscriptionType == SubscriptionType.Premium &&
                   (!currentUser.Subscription.ExpiresAt.HasValue ||
                    currentUser.Subscription.ExpiresAt > DateTime.UtcNow);
        }

        private static List<User> ApplyPremiumFilters(List<User> users, User currentUser)
        {
            var preferences = currentUser.Preferences;
            if (preferences == null) return users;

            return users.Where(u =>
            {
                // Height range filters
                if (preferences.PreferredHeightMin.HasValue && u.HeightCm < preferences.PreferredHeightMin.Value)
                    return false;
                if (preferences.PreferredHeightMax.HasValue && u.HeightCm > preferences.PreferredHeightMax.Value)
                    return false;

                // Education filter
                if (preferences.PreferredEducationLevels?.Count > 0)
                {
                    if (!u.EducationLevel.HasValue || !preferences.PreferredEducationLevels.Contains(u.EducationLevel.Value))
                        return false;
                }

                // Children filters
                if (preferences.PreferredChildren?.Count > 0)
                {
                    if (!u.Children.HasValue || !preferences.PreferredChildren.Contains(u.Children.Value))
                        return false;
                }
                if (preferences.PreferredFamilyPlans?.Count > 0)
                {
                    if (!u.FamilyPlan.HasValue || !preferences.PreferredFamilyPlans.Contains(u.FamilyPlan.Value))
                        return false;
                }

                // Vices filters
                if (preferences.PreferredDrugs?.Count > 0)
                {
                    if (!u.Drugs.HasValue || !preferences.PreferredDrugs.Contains(u.Drugs.Value))
                        return false;
                }

                if (preferences.PreferredSmoking?.Count > 0)
                {
                    if (!u.Smoking.HasValue || !preferences.PreferredSmoking.Contains(u.Smoking.Value))
                        return false;
                }

                if (preferences.PreferredMarijuana?.Count > 0)
                {
                    if (!u.Marijuana.HasValue || !preferences.PreferredMarijuana.Contains(u.Marijuana.Value))
                        return false;
                }

                if (preferences.PreferredDrinking?.Count > 0)
                {
                    if (!u.Drinking.HasValue || !preferences.PreferredDrinking.Contains(u.Drinking.Value))
                        return false;
                }

                return true;
            }).ToList();
        }
        #endregion
    }

    public class CachedDiscoveryData
    {
        public List<DiscoveryUserDTO> Users { get; set; } = [];
        public string DiscoveryMode { get; set; } = string.Empty;
        public int RequestedCount { get; set; }
        public int ActualCount { get; set; }
        public bool FromCache { get; set; }
        public DateTime CachedAt { get; set; }
        public decimal? UserLatitude { get; set; }
        public decimal? UserLongitude { get; set; }
        public bool HasMore { get; set; } = false;
    }
}