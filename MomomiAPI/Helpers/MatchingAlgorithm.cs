using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;

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
        /// </summary>
        public async Task<List<UserProfileDTO>> GetPotentialMatchesAsync(Guid userId, int maxResults = 20)
        {
            try
            {
                var currentUser = await _dbContext.Users.Include(u => u.Preferences)
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

                return rankedMatches.Take(maxResults).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting potential matches for user {UserId}", userId);
                return [];
            }
        }

        public async Task<List<User>> GetFilteredCandidatesAsync(User currentUser, List<Guid> excludedUserIds)
        {
            var query = _dbContext.Users.Include(u => u.Photos).Include(u => u.Preferences).Where(u => u.IsActive && !excludedUserIds.Contains(u.Id));

            // Basic filters
            if (currentUser.InterestedIn.HasValue)
            {
                query = query.Where(u => u.Gender == currentUser.InterestedIn);
            }

            // Age filters
            if (currentUser.DateOfBirth.HasValue)
            {
                var userAge = DateTime.UtcNow.Year - currentUser.DateOfBirth.Value.Year;
                var minBirthYear = DateTime.UtcNow.Year - currentUser.MaxAge;
                var maxBirthYear = DateTime.UtcNow.Year - currentUser.MinAge;

                query = query.Where(u => u.DateOfBirth.HasValue &&
                    u.DateOfBirth.Value.Year >= minBirthYear &&
                    u.DateOfBirth.Value.Year <= maxBirthYear);
            }

            // Distance filter (if location available)
            if (currentUser.Latitude.HasValue && currentUser.Longitude.HasValue)
            {
                // For now, we will filter in memore. In production use spatial queries
                var allCandidates = await query.ToListAsync();

                return allCandidates.Where(u => u.Latitude.HasValue && u.Longitude.HasValue &&
                LocationHelper.CalculateDistance(
                    (double)currentUser.Latitude, (double)currentUser.Longitude, (double)u.Latitude, (double)u.Longitude) <= currentUser.MaxDistanceKm).ToList();
            }

            return await query.ToListAsync();
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

                // Add distance if available
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
            return rankedMatches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Profile.DistanceKm ?? double.MaxValue) // Closer users as tiebreaker
                .Select(m => m.Profile)
                .ToList();
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
