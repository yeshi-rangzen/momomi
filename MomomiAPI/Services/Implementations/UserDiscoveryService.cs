using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class UserDiscoveryService : IUserDiscoveryService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly IReportingService _reportingService;
        private readonly MatchingAlgorithm _matchingAlgorithm;
        private readonly ILogger<UserDiscoveryService> _logger;

        public UserDiscoveryService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            IReportingService reportingService,
            MatchingAlgorithm matchingAlgorithm,
            ILogger<UserDiscoveryService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _reportingService = reportingService;
            _matchingAlgorithm = matchingAlgorithm;
            _logger = logger;
        }

        public async Task<DiscoveryResult> FindUsersForSwiping(Guid userId, int count = 10)
        {
            try
            {
                if (count < 1 || count > 50)
                {
                    return (DiscoveryResult)DiscoveryResult.Failed("Count must be between 1 and 50.");
                }

                var currentUser = await _dbContext.Users
                    .Include(u => u.Preferences)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (currentUser == null)
                {
                    return DiscoveryResult.UserNotFound();
                }

                // Route to appropriate discovery method based on user preference
                return currentUser.EnableGlobalDiscovery
                    ? await FindUsersGlobally(userId, count)
                    : await FindUsersLocally(userId, count, currentUser.MaxDistanceKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding users for swiping for user {UserId}", userId);
                return (DiscoveryResult)DiscoveryResult.Failed("Unable to find users at this time. Please try again.");
            }
        }

        public async Task<DiscoveryResult> FindUsersGlobally(Guid userId, int count = 10)
        {
            try
            {
                if (count < 1 || count > 50)
                {
                    return (DiscoveryResult)DiscoveryResult.Failed("Count must be between 1 and 50.");
                }

                var cacheKey = CacheKeys.Discovery.GlobalResults(userId, count);
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null && cachedUsers.Any())
                {
                    _logger.LogDebug("Returning cached global discovery results for user {UserId}", userId);
                    return DiscoveryResult.Success(cachedUsers, "global", count, true);
                }

                var discoveryUsers = await _matchingAlgorithm.GetPotentialMatchesAsync(userId, count);

                // Cache results
                await _cacheService.SetAsync(cacheKey, discoveryUsers, CacheKeys.Duration.DiscoveryResults);

                _logger.LogInformation("Found {Count} global users for user {UserId}", discoveryUsers.Count, userId);
                return DiscoveryResult.Success(discoveryUsers, "global", count, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding global users for user {UserId}", userId);
                return (DiscoveryResult)DiscoveryResult.Failed("Unable to find users globally. Please try again.");
            }
        }

        public async Task<DiscoveryResult> FindUsersLocally(Guid userId, int count = 10, int maxDistanceKm = 50)
        {
            try
            {
                if (count < 1 || count > 50)
                {
                    return (DiscoveryResult)DiscoveryResult.Failed("Count must be between 1 and 50.");
                }

                if (maxDistanceKm < 1 || maxDistanceKm > 200)
                {
                    return (DiscoveryResult)DiscoveryResult.Failed("Distance must be between 1 and 200 km.");
                }

                var currentUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (currentUser == null)
                {
                    return DiscoveryResult.UserNotFound();
                }

                if (currentUser.Latitude == null || currentUser.Longitude == null)
                {
                    return DiscoveryResult.LocationRequired();
                }

                var cacheKey = CacheKeys.Discovery.LocalResults(userId, count);
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null && cachedUsers.Any())
                {
                    _logger.LogDebug("Returning cached local discovery results for user {UserId}", userId);
                    return DiscoveryResult.Success(cachedUsers, "local", count, true);
                }

                var discoveryUsers = await _matchingAlgorithm.GetPotentialMatchesAsync(userId, count * 2); // Get more for distance filtering

                // Filter by distance
                var localUsers = discoveryUsers.Where(user =>
                    user.DistanceKm.HasValue && user.DistanceKm <= maxDistanceKm)
                    .Take(count)
                    .ToList();

                // Cache results
                await _cacheService.SetAsync(cacheKey, localUsers, CacheKeys.Duration.DiscoveryResults);

                _logger.LogInformation("Found {Count} local users within {Distance}km for user {UserId}",
                    localUsers.Count, maxDistanceKm, userId);

                return DiscoveryResult.Success(localUsers, "local", count, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding local users for user {UserId}", userId);
                return (DiscoveryResult)DiscoveryResult.Failed("Unable to find nearby users. Please try again.");
            }
        }
    }
}