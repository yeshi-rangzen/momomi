using CloudinaryDotNet;
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
        private readonly Cloudinary _cloudinary;

        public UserService(MomomiDbContext dbContext, ILogger<UserService> logger, Cloudinary cloudinary)
        {
            _dbContext = dbContext;
            _logger = logger;
            _cloudinary = cloudinary;
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
                    EnableGlobalDiscovery = user.EnableGlobalDiscovery,
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

                if (request.EnableGlobalDiscovery.HasValue)
                    user.EnableGlobalDiscovery = request.EnableGlobalDiscovery.Value;

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

        public async Task<bool> UpdateDiscoveryStatusAsync(Guid userId, bool isDiscoverable)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) return false;

                user.IsDiscoverable = isDiscoverable;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated discovery status for user {UserId} to {IsDiscoverable}", userId, isDiscoverable);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discovery status for user {UserId}", userId);
                return false;
            }
        }

        private async Task<IEnumerable<UserProfileDTO>> GetGlobalUsersAsync(Guid userId)
        {
            try
            {
                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser == null) return [];

                // Get users that haven't been liked/passed by current user
                var likedOrPassedUserIds = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .Select(ul => ul.LikedUserId)
                    .ToListAsync();

                // Get all active AND discoverable users except current user and already processed users
                var globalUsers = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Where(u => u.Id != userId &&
                               u.IsActive &&
                               u.IsDiscoverable &&
                               !likedOrPassedUserIds.Contains(u.Id)) // Exclude already liked/passed users
                    .Take(30) // Take only 30 users
                    .ToListAsync();

                var result = new List<UserProfileDTO>();

                foreach (var user in globalUsers)
                {
                    double? distance = null;

                    // Calculate distance if both users have location data
                    if (currentUser.Latitude.HasValue && currentUser.Longitude.HasValue &&
                        user.Latitude.HasValue && user.Longitude.HasValue)
                    {
                        distance = LocationHelper.CalculateDistance(
                            (double)currentUser.Latitude, (double)currentUser.Longitude,
                            (double)user.Latitude, (double)user.Longitude);
                    }

                    var profile = CreateUserProfileDTO(user, distance);
                    result.Add(profile);
                }

                // For global discovery, we can randomize or sort by other criteria
                return result.OrderBy(x => Guid.NewGuid()); // Random order
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving global users for user: {UserId}", userId);
                return Enumerable.Empty<UserProfileDTO>();
            }
        }

        /// <summary>
        /// Helper method to create UserProfileDTO from User entity
        /// </summary>
        private UserProfileDTO CreateUserProfileDTO(User user, double? distance = null)
        {
            return new UserProfileDTO
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
                EnableGlobalDiscovery = user.EnableGlobalDiscovery,
                IsVerified = user.IsVerified,
                LastActive = user.LastActive,
                Photos = user.Photos.Select(p => new UserPhotoDTO
                {
                    Id = p.Id,
                    Url = p.Url,
                    ThumbnailUrl = p.ThumbnailUrl,
                    PhotoOrder = p.PhotoOrder,
                    IsPrimary = p.IsPrimary
                }).OrderBy(p => p.PhotoOrder).ToList() ?? new List<UserPhotoDTO>()
            };
        }

        public async Task<IEnumerable<UserProfileDTO>> GetNearbyUsersAsync(Guid userId, int maxDistance)
        {
            try
            {
                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser == null)
                {
                    return [];
                }

                // If global discovery is enabled, ignore location and distance
                if (currentUser.EnableGlobalDiscovery)
                {
                    _logger.LogInformation("Getting global users for user {UserId} (global discovery enabled)", userId);
                    return await GetGlobalUsersAsync(userId);
                }

                if (currentUser?.Latitude == null || currentUser.Longitude == null)
                {
                    return [];
                }

                // Get users that haven't been liked/passed by current user
                var likedOrPassedUserIds = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .Select(ul => ul.LikedUserId)
                    .ToListAsync();


                // Take more users initially since we'll filter by distance
                var nearbyUsers = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Where(u => u.Id != userId &&
                               u.IsActive &&
                               u.IsDiscoverable &&
                               !likedOrPassedUserIds.Contains(u.Id) && // Exclude already liked/passed users
                               u.Latitude != null &&
                               u.Longitude != null)
                    .Take(100) // Take more initially for distance filtering
                    .ToListAsync();

                var result = new List<UserProfileDTO>();

                foreach (var user in nearbyUsers)
                {
                    var distance = LocationHelper.CalculateDistance(
                        (double)currentUser.Latitude, (double)currentUser.Longitude,
                        (double)user.Latitude!, (double)user.Longitude!);

                    if (distance <= maxDistance)
                    {
                        var profile = CreateUserProfileDTO(user, distance);
                        result.Add(profile);
                    }

                    // Stop when we have 30 users
                    if (result.Count >= 30) break;
                }

                return result.OrderBy(u => u.DistanceKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nearby users for user: {UserId}", userId);
                return Enumerable.Empty<UserProfileDTO>();
            }
        }

        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var user = await _dbContext.Users
                        .Include(u => u.Photos)
                        .Include(u => u.Preferences)
                        .Include(u => u.LikesGiven)
                        .Include(u => u.LikesReceived)
                        .Include(u => u.ConversationsAsUser1)
                        .Include(u => u.ConversationsAsUser2)
                        .Include(u => u.MessagesSent)
                        .Include(u => u.Subscription)
                        .Include(u => u.UsageLimit)
                        .Include(u => u.Notifications)
                        .FirstOrDefaultAsync(u => u.Id == userId);

                    if (user == null) return false;

                    // 1. Delete user photos from Cloudinary first
                    foreach (var photo in user.Photos)
                    {
                        try
                        {
                            var deleteParams = new CloudinaryDotNet.Actions.DeletionParams(photo.CloudinaryPublicId);
                            await _cloudinary.DestroyAsync(deleteParams);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete photo {PhotoId} from Cloudinary", photo.Id);
                        }
                    }

                    // 2. Delete user photos from database
                    _dbContext.UserPhotos.RemoveRange(user.Photos);

                    // 3. Delete user preferences
                    if (user.Preferences != null)
                    {
                        _dbContext.UserPreferences.Remove(user.Preferences);
                    }

                    // 4. Delete likes given and received
                    _dbContext.UserLikes.RemoveRange(user.LikesGiven);
                    _dbContext.UserLikes.RemoveRange(user.LikesReceived);

                    // 5. Delete all conversations and messages where user is participant
                    var conversations = user.ConversationsAsUser1.Concat(user.ConversationsAsUser2).Distinct();
                    foreach (var conversation in conversations)
                    {
                        // Delete all messages in the conversation
                        var messages = await _dbContext.Messages
                            .Where(m => m.ConversationId == conversation.Id)
                            .ToListAsync();
                        _dbContext.Messages.RemoveRange(messages);

                        // Delete the conversation
                        _dbContext.Conversations.Remove(conversation);
                    }

                    // 6. Delete subscription and usage limits
                    if (user.Subscription != null)
                    {
                        _dbContext.UserSubscriptions.Remove(user.Subscription);
                    }

                    if (user.UsageLimit != null)
                    {
                        _dbContext.UserUsageLimits.Remove(user.UsageLimit);
                    }

                    // 7. Delete notifications
                    _dbContext.PushNotifications.RemoveRange(user.Notifications);

                    // 8. DO NOT DELETE: Reports and Blocks (as per requirement)
                    // These remain in the database for audit/safety purposes

                    // 9. Finally, delete the user
                    _dbContext.Users.Remove(user);

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully deleted user {UserId} and all associated data", userId);
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return false;
            }
        }
    }
}
