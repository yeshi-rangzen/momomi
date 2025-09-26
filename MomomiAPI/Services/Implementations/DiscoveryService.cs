using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using NetTopologySuite.Geometries;
using User = MomomiAPI.Models.Entities.User;

namespace MomomiAPI.Services.Implementations
{
    public class DiscoveryService : IDiscoveryService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<DiscoveryService> _logger;
        private readonly IUserService _userService;

        public DiscoveryService(
            MomomiDbContext dbContext,
            ILogger<DiscoveryService> logger,
            IUserService userService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _userService = userService;
        }

        public async Task<DiscoveryResult> DiscoverCandidatesAsync(Guid userId, int maxResults = 30)
        {
            var userProfileResult = await _userService.GetUserProfileAsync(userId);

            if (userProfileResult == null || userProfileResult.Data == null)
            {
                return DiscoveryResult.UserNotFound();
            }

            var userDto = userProfileResult.Data.User;
            var discoveryMode = userDto.EnableGlobalDiscovery ? "global" : "local";
            List<User> discoveredUsers;

            if (discoveryMode == "global")
            {
                discoveredUsers = await DiscoverGlobalCandidatesAsync(userDto, maxResults);
            }
            else
            {
                discoveredUsers = await DiscoverLocalCandidatesAsync(userDto, maxResults);
            }

            if (discoveredUsers.Count == 0)
            {
                return DiscoveryResult.NoUsersFound();
            }

            var discoveredUserDtos = discoveredUsers.Select(user => UserMapper.UserToDiscoveryDTO(user)).ToList();
            var hasMore = maxResults == discoveredUserDtos.Count;

            return DiscoveryResult.Successful(
                    discoveredUserDtos,
                    hasMore
                );
        }

        private async Task<List<User>> DiscoverLocalCandidatesAsync(UserDTO currentUser, int maxResults)
        {
            var query = BuildBaseCandidateQuery(currentUser).Where(u => u.Latitude != 0 && u.Longitude != 0);

            var userLng = currentUser.Longitude;
            var userLat = currentUser.Latitude;
            var maxDistanceMeters = currentUser.MaxDistanceKm * 1000;
            var currentLocation = new Point(userLng, userLat) { SRID = 4326 };

            var candidates = await query
                .Where(u => u.Location.Distance(currentLocation) <= maxDistanceMeters)
                .OrderBy(u => u.LastActive)
                .Take(30)
                .ToListAsync();

            // Apply preference filters
            var filteredCandidates = ApplyPreferenceFilters(candidates, currentUser);

            // Apply premium filters if user is a subscriber
            if (IsActiveSubscriber(currentUser))
            {
                filteredCandidates = ApplyPremiumFilters(filteredCandidates, currentUser);
            }

            var finalResults = filteredCandidates.Take(maxResults).ToList();

            _logger.LogInformation("Local discovery for user {UserId}: Found {Count}/{CandidatesSize} users within {Distance}km after all filtering",
                currentUser.Id, finalResults.Count, candidates.Count, currentUser.MaxDistanceKm);

            return finalResults;
        }

        private async Task<List<User>> DiscoverGlobalCandidatesAsync(UserDTO currentUser, int maxResults)
        {
            var query = BuildBaseCandidateQuery(currentUser);

            var candidates = await query.Where(u => u.IsGloballyDiscoverable).
                OrderByDescending(u => u.LastActive) // Prioritize recently active users
                .Take(maxResults * 3) // Get 3x more to account for preference filtering
                .ToListAsync(); ;


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

        private IQueryable<User> BuildBaseCandidateQuery(UserDTO currentUser)
        {
            // Age filters
            var today = DateTime.UtcNow.Date;
            var minBirthDate = today.AddYears(-currentUser.MaxAge);
            var maxBirthDate = today.AddYears(-currentUser.MinAge);

            var query = _dbContext.Users
                .Include(u => u.Preferences)
                .Include(u => u.Photos)
                .Where(u => u.IsActive && u.IsDiscoverable && u.Id != currentUser.Id)
                .Where(u => u.InterestedIn == currentUser.Gender)
                .Where(u => !_dbContext.UserSwipes.Any(s => s.SwiperUserId == currentUser.Id && s.SwipedUserId == u.Id))
                .Where(u => !_dbContext.UserReports.Any(r =>
                    (r.ReporterEmail == currentUser.Email && r.ReportedEmail == u.Email) ||
                    (r.ReportedEmail == currentUser.Email && r.ReporterEmail == u.Email)
                ))
                .Where(u => u.DateOfBirth >= minBirthDate && u.DateOfBirth <= maxBirthDate);

            return query;
        }

        #region Private Helper Methods

        private static List<User> ApplyPreferenceFilters(List<User> users, UserDTO currentUser)
        {
            var preferredHeritage = currentUser.Preferences?.PreferredHeritage?.ToList();
            var preferredReligions = currentUser.Preferences?.PreferredReligions?.ToList();

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

                return true;

            }).ToList();
        }

        private static bool IsActiveSubscriber(UserDTO currentUser)
        {
            return currentUser.Subscription?.SubscriptionType == SubscriptionType.Premium &&
                   (!currentUser.Subscription.ExpiresAt.HasValue ||
                    currentUser.Subscription.ExpiresAt > DateTime.UtcNow);
        }

        private static List<User> ApplyPremiumFilters(List<User> users, UserDTO currentUser)
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

                if (preferences.LanguagePreference?.Count > 0)
                {
                    if (u.LanguagesSpoken.Count > 0 && !u.LanguagesSpoken.Any(l => preferences.LanguagePreference.Contains(l)))
                        return false;
                }

                return true;
            }).ToList();
        }
        #endregion
    }

}