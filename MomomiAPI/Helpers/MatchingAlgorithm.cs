using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Helpers
{
    public class MatchingAlgorithm
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<MatchingAlgorithm> _logger;

        public MatchingAlgorithm(MomomiDbContext dbContext, ILogger<MatchingAlgorithm> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Gets potential matches for a user with cultural compatibility scoring.
        /// Supports both global and location-based discovery.
        /// </summary>
        public async Task<List<UserProfileDTO>> GetPotentialMatchesAsync(Guid userId, int maxResults = 20)
        {
            try
            {
                var currentUser = await _dbContext.Users
                    .Include(u => u.Preferences)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (currentUser == null) return [];

                // Get users that haven't been liked/passed by current user
                var excludedUserIds = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .Select(ul => ul.LikedUserId)
                    .ToListAsync();

                excludedUserIds.Add(userId); // Exclude current user

                // Get potential matches based on preferences
                var potentialMatches = await GetFilteredCandidatesAsync(currentUser, excludedUserIds);

                // Calculate compatibility scores and rank
                var rankedMatches = await RankMatchesByCulturalCompatibilityAsync(currentUser, potentialMatches);

                return [.. rankedMatches.Take(maxResults)];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting potential matches for user {UserId}", userId);
                return [];
            }
        }

        public async Task<List<User>> GetFilteredCandidatesAsync(User currentUser, List<Guid> excludedUserIds)
        {
            var query = _dbContext.Users
                .Include(u => u.Photos)
                .Include(u => u.Preferences)
                .Where(u => u.IsActive && u.IsDiscoverable && !excludedUserIds.Contains(u.Id));

            // MEMBER FILTERS (Available to all users - Free and Premium)
            query = ApplyMemberFilters(query, currentUser);

            // SUBSCRIBER FILTERS (Premium users only)
            var isSubscriber = currentUser.Subscription?.SubscriptionType == SubscriptionType.Premium &&
                currentUser.Subscription.IsActive &&
                (!currentUser.Subscription.ExpiresAt.HasValue || currentUser.Subscription.ExpiresAt > DateTime.UtcNow);

            if (isSubscriber)
            {
                query = ApplySubscriberFilters(query, currentUser);
            }

            // Apply distance filter only if global discovery is disabled
            if (!currentUser.EnableGlobalDiscovery && currentUser.Latitude.HasValue && currentUser.Longitude.HasValue)
            {
                var localCandidates = await query.Take(100).ToListAsync(); // Get more for distance filtering

                var filteredByDistance = localCandidates.Where(u => u.Latitude.HasValue && u.Longitude.HasValue &&
                    LocationHelper.CalculateDistance(
                        (double)currentUser.Latitude, (double)currentUser.Longitude,
                        (double)u.Latitude, (double)u.Longitude) <= currentUser.MaxDistanceKm)
                    .Take(30) // Take only 30 after distance filtering
                    .ToList();

                _logger.LogInformation("Local discovery for user {UserId}: Found {Count} users within {Distance}km",
                    currentUser.Id, filteredByDistance.Count, currentUser.MaxDistanceKm);

                return filteredByDistance;
            }
            else
            {
                // Global discovery: just take first 30 users
                var globalCandidates = await query.Take(30).ToListAsync();

                _logger.LogInformation("Global discovery for user {UserId}: Found {Count} users",
                    currentUser.Id, globalCandidates.Count);

                return globalCandidates;
            }
        }

        private IQueryable<User> ApplyMemberFilters(IQueryable<User> query, User currentUser)
        {
            // Interested Gender Filters
            if (currentUser.InterestedIn.HasValue)
            {
                query = query.Where(u => u.Gender == currentUser.InterestedIn);
            }

            // Age filters
            if (currentUser.DateOfBirth.HasValue)
            {
                var minBirthYear = DateTime.UtcNow.Year - currentUser.MaxAge;
                var maxBirthYear = DateTime.UtcNow.Year - currentUser.MinAge;

                query = query.Where(u => u.DateOfBirth.HasValue &&
                    u.DateOfBirth.Value.Year >= minBirthYear &&
                    u.DateOfBirth.Value.Year <= maxBirthYear);
            }

            // Heritage filters (if user has preferences)
            if (currentUser.Preferences?.PreferredHeritage != null && currentUser.Preferences.PreferredHeritage.Any())
            {
                query = query.Where(u => u.Heritage != null &&
                    u.Heritage.Any(h => currentUser.Preferences.PreferredHeritage.Contains(h)));
            }

            // Religion filters (if user has preferences)
            if (currentUser.Preferences?.PreferredReligions != null && currentUser.Preferences.PreferredReligions.Any())
            {
                query = query.Where(u => u.Religion != null &&
                    u.Religion.Any(r => currentUser.Preferences.PreferredReligions.Contains(r)));
            }

            return query;
        }

        private IQueryable<User> ApplySubscriberFilters(IQueryable<User> query, User currentUser)
        {
            if (currentUser.Preferences == null) return query;

            // Height filters
            if (currentUser.Preferences.PreferredHeightMin.HasValue)
            {
                query = query.Where(u => u.HeightCm >= currentUser.Preferences.PreferredHeightMin);
            }

            if (currentUser.Preferences.PreferredHeightMax.HasValue)
            {
                query = query.Where(u => u.HeightCm <= currentUser.Preferences.PreferredHeightMax);
            }

            // Children status filters
            if (currentUser.Preferences.PreferredChildren != null && currentUser.Preferences.PreferredChildren.Any())
            {
                query = query.Where(u => u.Children.HasValue &&
                    currentUser.Preferences.PreferredChildren.Contains(u.Children.Value));
            }

            // Family plans filters
            if (currentUser.Preferences.PreferredFamilyPlans != null && currentUser.Preferences.PreferredFamilyPlans.Any())
            {
                query = query.Where(u => u.FamilyPlan.HasValue &&
                    currentUser.Preferences.PreferredFamilyPlans.Contains(u.FamilyPlan.Value));
            }

            // Drugs filters
            if (currentUser.Preferences.PreferredDrugs != null && currentUser.Preferences.PreferredDrugs.Any())
            {
                query = query.Where(u => u.Drugs.HasValue &&
                    currentUser.Preferences.PreferredDrugs.Contains(u.Drugs.Value));
            }

            // Smoking filters
            if (currentUser.Preferences.PreferredSmoking != null && currentUser.Preferences.PreferredSmoking.Any())
            {
                query = query.Where(u => u.Smoking.HasValue &&
                    currentUser.Preferences.PreferredSmoking.Contains(u.Smoking.Value));
            }

            // Marijuana filters
            if (currentUser.Preferences.PreferredMarijuana != null && currentUser.Preferences.PreferredMarijuana.Any())
            {
                query = query.Where(u => u.Marijuana.HasValue &&
                    currentUser.Preferences.PreferredMarijuana.Contains(u.Marijuana.Value));
            }

            // Drinking filters
            if (currentUser.Preferences.PreferredDrinking != null && currentUser.Preferences.PreferredDrinking.Any())
            {
                query = query.Where(u => u.Drinking.HasValue &&
                    currentUser.Preferences.PreferredDrinking.Contains(u.Drinking.Value));
            }

            // Education level filters
            if (currentUser.Preferences.PreferredEducationLevels != null && currentUser.Preferences.PreferredEducationLevels.Any())
            {
                query = query.Where(u => u.EducationLevel.HasValue &&
                    currentUser.Preferences.PreferredEducationLevels.Contains(u.EducationLevel.Value));
            }

            return query;
        }

        private async Task<List<UserProfileDTO>> RankMatchesByCulturalCompatibilityAsync(
            User currentUser, List<User> candidates)
        {
            var rankedMatches = new List<(UserProfileDTO Profile, double Score)>();

            foreach (var candidate in candidates)
            {
                var compatibilityScore = CulturalCompatibility.CalculateCompatibilityScore(currentUser, candidate);

                var profile = new UserProfileDTO
                {
                    Id = candidate.Id,
                    FirstName = candidate.FirstName,
                    LastName = candidate.LastName,
                    Age = candidate.DateOfBirth.HasValue ?
                        DateTime.UtcNow.Year - candidate.DateOfBirth.Value.Year : 0,
                    Gender = candidate.Gender,
                    Bio = candidate.Bio,
                    Heritage = candidate.Heritage,
                    Religion = candidate.Religion,
                    LanguagesSpoken = candidate.LanguagesSpoken,
                    EducationLevel = candidate.EducationLevel,
                    Occupation = candidate.Occupation,
                    HeightCm = candidate.HeightCm,
                    Hometown = candidate.Hometown,
                    Children = candidate.Children,
                    FamilyPlan = candidate.FamilyPlan,
                    Drugs = candidate.Drugs,
                    Smoking = candidate.Smoking,
                    Marijuana = candidate.Marijuana,
                    Drinking = candidate.Drinking,
                    IsVerified = candidate.IsVerified,
                    LastActive = candidate.LastActive,
                    Photos = candidate.Photos.Select(p => new UserPhotoDTO
                    {
                        Id = p.Id,
                        Url = p.Url,
                        ThumbnailUrl = p.ThumbnailUrl,
                        PhotoOrder = p.PhotoOrder,
                        IsPrimary = p.IsPrimary
                    }).OrderBy(p => p.PhotoOrder).ToList()
                };

                // Add distance if both users have location data
                if (currentUser.Latitude.HasValue && currentUser.Longitude.HasValue &&
                    candidate.Latitude.HasValue && candidate.Longitude.HasValue)
                {
                    profile.DistanceKm = LocationHelper.CalculateDistance(
                        (double)currentUser.Latitude, (double)currentUser.Longitude,
                        (double)candidate.Latitude, (double)candidate.Longitude);
                }

                rankedMatches.Add((profile, compatibilityScore));
            }

            // Sort by compatibility score (highest first)
            // For global discovery, prioritize compatibility over distance
            // For local discovery, use distance as tiebreaker
            if (currentUser.EnableGlobalDiscovery)
            {
                // Global discovery: Random order after compatibility sorting
                return rankedMatches
                    .OrderByDescending(m => m.Score)
                    .ThenBy(m => Guid.NewGuid()) // Random order for diversity
                    .Select(m => m.Profile)
                    .ToList();
            }
            else
            {
                // Local discovery: Distance as tiebreaker
                return rankedMatches
                    .OrderByDescending(m => m.Score)
                    .ThenBy(m => m.Profile.DistanceKm ?? double.MaxValue) // Closer users first
                    .Select(m => m.Profile)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if two users are a match (both liked each other)
        /// </summary>
        public async Task<bool> CheckForMatchAsync(Guid userId1, Guid userId2)
        {
            var like1 = await _dbContext.UserLikes
                .FirstOrDefaultAsync(ul => ul.LikerUserId == userId1 &&
                                         ul.LikedUserId == userId2 && ul.IsLike);

            var like2 = await _dbContext.UserLikes
                .FirstOrDefaultAsync(ul => ul.LikerUserId == userId2 &&
                                         ul.LikedUserId == userId1 && ul.IsLike);

            return like1 != null && like2 != null;
        }

        /// <summary>
        /// Creates a match and conversation when two users like each other
        /// </summary>
        public async Task<bool> CreateMatchAsync(Guid userId1, Guid userId2)
        {
            try
            {
                // Update like records to indicate match
                var likes = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId1 && ul.LikedUserId == userId2) ||
                               (ul.LikerUserId == userId2 && ul.LikedUserId == userId1))
                    .ToListAsync();

                foreach (var like in likes)
                {
                    like.IsMatch = true;
                }

                // Create conversation
                var conversation = new Conversation
                {
                    User1Id = userId1 < userId2 ? userId1 : userId2, // Ensure consistent ordering
                    User2Id = userId1 < userId2 ? userId2 : userId1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Conversations.Add(conversation);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating match between users {UserId1} and {UserId2}", userId1, userId2);
                return false;
            }
        }
    }
}
