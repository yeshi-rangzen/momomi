using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace MomomiAPI.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<UserService> _logger;

        public UserService(MomomiDbContext dbContext, ILogger<UserService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            try
            {
                return await _dbContext.Users.Include(u => u.Photos).Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<User?> GetUserBySupabaseUidAsync(Guid supabaseUid)
        {
            try
            {
                return await _dbContext.Users.Include(u => u.Photos).Include(u => u.Preferences).FirstOrDefaultAsync(u => u.SupabaseUid == supabaseUid && u.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by Supabase UID: {SupabaseUid}", supabaseUid);
                return null;
            }
        }

        public async Task<User?> ValidateAndGetUserAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return null;

                // Parse token without validation to get claims
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);

                var supabaseUid = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                if (string.IsNullOrEmpty(supabaseUid) || !Guid.TryParse(supabaseUid, out var uid))
                    return null;

                return await GetUserBySupabaseUidAsync(uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token and getting user");
                return null;
            }
        }

        public async Task<UserProfileDTO?> GetUserProfileAsync(Guid userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null) return null;
                return new UserProfileDTO
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Age = user.DateOfBirth.HasValue ? DateTime.UtcNow.Year - user.DateOfBirth.Value.Year : 0,
                    Gender = user.Gender,
                    Bio = user.Bio,
                    Heritage = user.Heritage,
                    Religion = user.Religion,
                    LanguagesSpoken = user.LanguagesSpoken,
                    EducationLevel = user.EducationLevel,
                    Occupation = user.Occupation,
                    HeightCm = user.HeightCm,
                    IsVerified = user.IsVerified,
                    LastActive = user.LastActive,
                    Photos = user.Photos.Select(p => new UserPhotoDTO
                    {
                        Id = p.Id,
                        Url = p.Url,
                        ThumbnailUrl = p.ThumbnailUrl,
                        PhotoOrder = p.PhotoOrder,
                        IsPrimary = p.IsPrimary
                    }).OrderBy(p => p.PhotoOrder).ToList(),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile: {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _dbContext.Users.Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return false;

                // Update user properties based on the request
                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;

                if (!string.IsNullOrEmpty(request.Bio))
                    user.Bio = request.Bio;

                if (request.Heritage.HasValue)
                    user.Heritage = request.Heritage.Value;

                if (request.Religion.HasValue)
                    user.Religion = request.Religion.Value;

                if (request.LanguagesSpoken != null)
                    user.LanguagesSpoken = request.LanguagesSpoken;

                if (!string.IsNullOrEmpty(request.EducationLevel))
                    user.EducationLevel = request.EducationLevel;

                if (!string.IsNullOrEmpty(request.Occupation))
                    user.Occupation = request.Occupation;

                if (request.HeightCm.HasValue)
                    user.HeightCm = request.HeightCm.Value;

                if (request.Latitude.HasValue)
                    user.Latitude = (decimal)request.Latitude.Value;

                if (request.Longitude.HasValue)
                    user.Longitude = (decimal)request.Longitude.Value;

                if (request.MaxDistanceKm.HasValue)
                    user.MaxDistanceKm = request.MaxDistanceKm.Value;

                if (request.MinAge.HasValue)
                    user.MinAge = request.MinAge.Value;

                if (request.MaxAge.HasValue)
                    user.MaxAge = request.MaxAge.Value;

                // Update or create preferences
                if (user.Preferences == null)
                {
                    user.Preferences = new UserPreference
                    {
                        UserId = user.Id,
                        CreatedAt = DateTime.UtcNow,
                    };
                }
                if (request.PreferredHeritage != null)
                    user.Preferences.PreferredHeritage = request.PreferredHeritage;

                if (request.PreferredReligions != null)
                    user.Preferences.PreferredReligions = request.PreferredReligions;

                if (request.CulturalImportanceLevel.HasValue)
                    user.Preferences.CulturalImportanceLevel = request.CulturalImportanceLevel.Value;

                if (request.LanguagePreference != null)
                    user.Preferences.LanguagePreference = request.LanguagePreference;

                user.UpdatedAt = DateTime.UtcNow;
                user.Preferences.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> DeactivateUserAsync(Guid userId)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) return false;

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
                return false;
            }
        }

        public async Task<IEnumerable<UserProfileDTO>> GetNearbyUsersAsync(Guid userId, int maxDistance)
        {
            try
            {
                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser?.Latitude == null || currentUser.Longitude == null)
                {
                    return [];
                }

                // Simple distance calculation - in production use proper geospatial queries
                var nearbyUsers = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Where(u => u.Id != userId &&
                               u.IsActive &&
                               u.Latitude != null &&
                               u.Longitude != null)
                    .ToListAsync();

                var result = new List<UserProfileDTO>();

                foreach (var user in nearbyUsers)
                {
                    var distance = LocationHelper.CalculateDistance(
                        (double)currentUser.Latitude, (double)currentUser.Longitude,
                        (double)user.Latitude!, (double)user.Longitude!);

                    if (distance <= maxDistance)
                    {
                        var profile = new UserProfileDTO
                        {
                            Id = user.Id,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            Age = user.DateOfBirth.HasValue ?
                                DateTime.UtcNow.Year - user.DateOfBirth.Value.Year : 0,
                            Gender = user.Gender,
                            Bio = user.Bio,
                            Heritage = user.Heritage,
                            Religion = user.Religion,
                            LanguagesSpoken = user.LanguagesSpoken,
                            EducationLevel = user.EducationLevel,
                            Occupation = user.Occupation,
                            HeightCm = user.HeightCm,
                            DistanceKm = distance,
                            IsVerified = user.IsVerified,
                            LastActive = user.LastActive,
                            Photos = user.Photos.Select(p => new UserPhotoDTO
                            {
                                Id = p.Id,
                                Url = p.Url,
                                ThumbnailUrl = p.ThumbnailUrl,
                                PhotoOrder = p.PhotoOrder,
                                IsPrimary = p.IsPrimary
                            }).OrderBy(p => p.PhotoOrder).ToList()
                        };
                        result.Add(profile);
                    }
                }
                return result.OrderBy(u => u.DistanceKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nearby users for user: {UserId}", userId);
                return Enumerable.Empty<UserProfileDTO>();
            }
        }

    }
}
