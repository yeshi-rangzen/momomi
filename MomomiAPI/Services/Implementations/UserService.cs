using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly Supabase.Client _supabaseClient;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<UserService> _logger;

        public UserService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            ILogger<UserService> logger,
            Supabase.Client supabaseClient)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
            _supabaseClient = supabaseClient;
        }

        public async Task<OperationResult<User>> GetUserByIdAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

                var user = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Include(u => u.Preferences)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return OperationResult<User>.NotFound("User not found or inactive");
                }
                return OperationResult<User>.Successful(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID: {UserId}", userId);
                return OperationResult<User>.Failed("Unable to retrieve user. Please try again.");
            }
        }

        public async Task<OperationResult<User>> GetUserBySupabaseUidAsync(Guid supabaseUid)
        {
            try
            {
                _logger.LogDebug("Retrieving user by Supabase UID: {SupabaseUID}", supabaseUid);

                var user = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Include(u => u.Preferences)
                    .FirstOrDefaultAsync(u => u.SupabaseUid == supabaseUid && u.IsActive);

                if (user == null)
                {
                    return OperationResult<User>.NotFound("User not found or inactive");
                }

                return OperationResult<User>.Successful(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by Supabase UID: {SupabaseUid}", supabaseUid);
                return OperationResult<User>.Failed("Unable to retrieve user. Please try again.");
            }
        }

        public async Task<OperationResult<UserProfileDTO>> GetUserProfileAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving user profile: {UserId}", userId);

                var cacheKey = CacheKeys.Users.Profile(userId);
                var cachedProfile = await _cacheService.GetAsync<UserProfileDTO>(cacheKey);

                if (cachedProfile != null)
                {
                    _logger.LogDebug("Returning cached profile for user {UserId}", userId);
                    return OperationResult<UserProfileDTO>.Successful(cachedProfile);
                }

                var userResult = await GetUserByIdAsync(userId);
                if (!userResult.Success)
                {
                    return OperationResult<UserProfileDTO>.NotFound("User profile not found.");
                }

                var user = userResult.Data!;
                var profile = MapUserToProfileDTO(user);

                // Cache the profile
                await _cacheService.SetAsync(cacheKey, profile, CacheKeys.Duration.UserProfile);

                _logger.LogDebug("Retrieved and cached profile for user {UserId}", userId);
                return OperationResult<UserProfileDTO>.Successful(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile: {UserId}", userId);
                return OperationResult<UserProfileDTO>.Failed("Unable to retrieve user profile. Please try again."); ;
            }
        }

        public async Task<OperationResult> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                _logger.LogInformation("Updating user profile: {UserId}", userId);

                // Validate request
                var validationResult = ValidateUpdateProfileRequest(request);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                var user = await _dbContext.Users
                    .Include(u => u.Preferences)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }

                // Update basic user properties
                UpdateBasicUserProperties(user, request);

                // Update or create preferences
                UpdateUserPreferences(user, request);

                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Invalidate caches
                await _cacheInvalidation.InvalidateUserProfile(userId);
                await _cacheInvalidation.InvalidateUserDiscovery(userId);

                _logger.LogInformation("Successfully updated profile for user {UserId}", userId);
                return OperationResult.Successful().WithMetadata("updated_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile: {UserId}", userId);
                return OperationResult.Failed("Unable to update profile. Please try again.");
            }
        }

        public async Task<OperationResult> DeactivateUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deactivate user: {UserId}", userId);

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }

                if (!user.IsActive)
                {
                    return OperationResult.BusinessRuleViolation("User is already inactive.");
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Invalidate all user-related caches
                await _cacheInvalidation.InvalidateUserRelatedCaches(userId);

                _logger.LogInformation("Successfully deactivating user {UserId}", userId);
                return OperationResult.Successful().WithMetadata("deactivated_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
                return OperationResult.Failed("Unable to deactivate account. Please try again.");
            }
        }

        public async Task<OperationResult> UpdateDiscoveryStatusAsync(Guid userId, bool isDiscoverable)
        {
            try
            {
                _logger.LogInformation("Updating discovery status for user {UserId} to {IsDiscoverable}", userId, isDiscoverable);

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return OperationResult.NotFound("User not found");
                }

                user.IsDiscoverable = isDiscoverable;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated discovery status for user {UserId} to {IsDiscoverable}", userId, isDiscoverable);
                return OperationResult.Successful()
                    .WithMetadata("is_discoverable", isDiscoverable)
                    .WithMetadata("updated_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discovery status for user {UserId}", userId);
                return OperationResult.Failed("Unable to update discovery settings. Please try again.");
            }
        }

        public async Task<OperationResult<List<UserProfileDTO>>> GetNearbyUsersAsync(Guid userId, int maxDistance)
        {
            try
            {
                _logger.LogInformation("Getting nearby users for user {UserId} within {MaxDistance}km", userId, maxDistance);

                var currentUser = await _dbContext.Users.FindAsync(userId);
                if (currentUser == null)
                {
                    return OperationResult<List<UserProfileDTO>>.NotFound("Current user not found.");
                }

                // If global discovery is enabled, ignore location and distance
                if (currentUser.EnableGlobalDiscovery)
                {
                    _logger.LogDebug("Getting global users for user {UserId} (global discovery enabled)", userId);
                    return await GetGlobalUsersAsync(userId);
                }

                if (currentUser?.Latitude == null || currentUser.Longitude == null)
                {
                    return OperationResult<List<UserProfileDTO>>.BusinessRuleViolation(
                        "Location is required for nearby user discovery.");
                }

                var cacheKey = CacheKeys.Discovery.LocalResults(userId, 30);
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null)
                {
                    _logger.LogDebug("Returning cached nearby users for user {UserId}", userId);
                    return OperationResult<List<UserProfileDTO>>.Successful(cachedUsers);
                }

                // Get users that haven't been liked/passed by current user
                var excludedUserIds = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .Select(ul => ul.LikedUserId)
                    .ToListAsync();

                excludedUserIds.Add(userId);

                // Get nearby users
                var nearbyUsers = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Where(u => !excludedUserIds.Contains(u.Id) &&
                                u.Id != userId &&
                                u.IsActive &&
                                u.IsDiscoverable &&
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
                        var profile = MapUserToProfileDTO(user, distance);
                        result.Add(profile);
                    }

                    // Stop when we have 30 users
                    if (result.Count >= 30) break;
                }

                var orderedResult = result.OrderBy(u => u.DistanceKm).ToList();

                // Cache results
                await _cacheService.SetAsync(cacheKey, orderedResult, CacheKeys.Duration.DiscoveryResults);

                _logger.LogInformation("Found {Count} nearby users for user {UserId}", orderedResult.Count, userId);
                return OperationResult<List<UserProfileDTO>>.Successful(orderedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nearby users for user: {UserId}", userId);
                return OperationResult<List<UserProfileDTO>>.Failed("Unable to find nearby users. Please try again.");
            }
        }

        public async Task<OperationResult> DeleteUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting user account: {UserId}", userId);

                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
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
                            .Include(u => u.ReportsMade)
                            .FirstOrDefaultAsync(u => u.Id == userId);

                        if (user == null)
                        {
                            return OperationResult.NotFound("User not found.");
                        }

                        // 1. Delete user photos from Storage first
                        var photoDeleteTasks = user.Photos.Select(async photo =>
                        {
                            try
                            {
                                await _supabaseClient.Storage
                                    .From("user-photos")
                                    .Remove(new List<string> { photo.StoragePath });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete photo {PhotoId} from Storage", photo.Id);
                            }
                        });

                        await Task.WhenAll(photoDeleteTasks);

                        // Delete database records in correct order
                        await DeleteUserRelatedData(user);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Invalidate all user-related caches
                        await _cacheInvalidation.InvalidateUserRelatedCaches(userId);

                        _logger.LogInformation("Successfully deleted user {UserId} and all associated data", userId);
                        return OperationResult.Successful().WithMetadata("deleted_at", DateTime.UtcNow);
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return OperationResult.Failed("Unable to delete account. Please try again.");
            }
        }

        public async Task<OperationResult<DiscoverySettingsDTO>> GetDiscoverySettingsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting discovery settings for user {UserId}", userId);

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return OperationResult<DiscoverySettingsDTO>.NotFound("User not found.");
                }

                var discoverySettings = new DiscoverySettingsDTO
                {
                    EnableGlobalDiscovery = user.EnableGlobalDiscovery,
                    IsDiscoverable = user.IsDiscoverable,
                    IsGloballyDiscoverable = user.IsGloballyDiscoverable,
                    MaxDistanceKm = user.MaxDistanceKm,
                    MinAge = user.MinAge,
                    MaxAge = user.MaxAge,
                    HasLocation = user.Latitude.HasValue && user.Longitude.HasValue
                };

                _logger.LogDebug("Retrieved discovery settings for user {UserId}", userId);
                return OperationResult<DiscoverySettingsDTO>.Successful(discoverySettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery settings for user {UserId}", userId);
                return OperationResult<DiscoverySettingsDTO>.Failed("Unable to retrieve discovery settings. Please try again.");
            }
        }

        public async Task<OperationResult<DiscoverySettingsDTO>> UpdateDiscoverySettingsAsync(Guid userId, UpdateDiscoverySettingsRequest request)
        {
            try
            {
                _logger.LogInformation("Updating discovery settings for user {UserId}", userId);

                // Validate request
                var validationResult = ValidateUpdateDiscoverySettingsRequest(request);
                if (!validationResult.Success)
                {
                    return OperationResult<DiscoverySettingsDTO>.ValidationFailure(validationResult.ErrorMessage!);
                }

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return OperationResult<DiscoverySettingsDTO>.NotFound("User not found.");
                }

                // Update discovery settings
                if (request.EnableGlobalDiscovery.HasValue)
                {
                    user.EnableGlobalDiscovery = request.EnableGlobalDiscovery.Value;
                }

                if (request.IsDiscoverable.HasValue)
                {
                    user.IsDiscoverable = request.IsDiscoverable.Value;
                }

                if (request.IsGloballyDiscoverable.HasValue)
                {
                    user.IsGloballyDiscoverable = request.IsGloballyDiscoverable.Value;
                }

                if (request.MaxDistanceKm.HasValue)
                {
                    user.MaxDistanceKm = request.MaxDistanceKm.Value;
                }

                if (request.MinAge.HasValue)
                {
                    user.MinAge = request.MinAge.Value;
                }

                if (request.MaxAge.HasValue)
                {
                    user.MaxAge = request.MaxAge.Value;
                }

                // Update location if provided
                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    user.Latitude = (decimal)request.Latitude.Value;
                    user.Longitude = (decimal)request.Longitude.Value;
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Invalidate relevant caches
                await _cacheInvalidation.InvalidateUserProfile(userId);
                await _cacheInvalidation.InvalidateUserDiscovery(userId);

                // Return updated settings
                var updatedSettings = new DiscoverySettingsDTO
                {
                    EnableGlobalDiscovery = user.EnableGlobalDiscovery,
                    IsDiscoverable = user.IsDiscoverable,
                    IsGloballyDiscoverable = user.IsGloballyDiscoverable,
                    MaxDistanceKm = user.MaxDistanceKm,
                    MinAge = user.MinAge,
                    MaxAge = user.MaxAge,
                    HasLocation = user.Latitude.HasValue && user.Longitude.HasValue
                };

                _logger.LogInformation("Successfully updated discovery settings for user {UserId}", userId);
                return OperationResult<DiscoverySettingsDTO>.Successful(updatedSettings)
                    .WithMetadata("updated_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discovery settings for user {UserId}", userId);
                return OperationResult<DiscoverySettingsDTO>.Failed("Unable to update discovery settings. Please try again.");
            }
        }

        private static OperationResult ValidateUpdateDiscoverySettingsRequest(UpdateDiscoverySettingsRequest request)
        {
            if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge >= request.MaxAge)
            {
                return OperationResult.ValidationFailure("Minimum age must be less than maximum age.");
            }

            if (request.MinAge.HasValue && request.MinAge < 18)
            {
                return OperationResult.ValidationFailure("Minimum age must be at least 18.");
            }

            if (request.MaxAge.HasValue && request.MaxAge > 100)
            {
                return OperationResult.ValidationFailure("Maximum age cannot exceed 100.");
            }

            if (request.MaxDistanceKm.HasValue && (request.MaxDistanceKm < 1 || request.MaxDistanceKm > 200))
            {
                return OperationResult.ValidationFailure("Maximum distance must be between 1 and 200 km.");
            }

            // Validate location coordinates if provided
            if (request.Latitude.HasValue || request.Longitude.HasValue)
            {
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    return OperationResult.ValidationFailure("Both latitude and longitude must be provided together.");
                }

                if (request.Latitude < -90 || request.Latitude > 90)
                {
                    return OperationResult.ValidationFailure("Latitude must be between -90 and 90.");
                }

                if (request.Longitude < -180 || request.Longitude > 180)
                {
                    return OperationResult.ValidationFailure("Longitude must be between -180 and 180.");
                }
            }

            return OperationResult.Successful();
        }

        private async Task<OperationResult<List<UserProfileDTO>>> GetGlobalUsersAsync(Guid userId)
        {
            try
            {
                var cacheKey = CacheKeys.Discovery.GlobalResults(userId, 30);
                var cachedUsers = await _cacheService.GetAsync<List<UserProfileDTO>>(cacheKey);

                if (cachedUsers != null)
                {
                    return OperationResult<List<UserProfileDTO>>.Successful(cachedUsers);
                }

                var excludedUserIds = await _dbContext.UserLikes
                    .Where(ul => ul.LikerUserId == userId)
                    .Select(ul => ul.LikedUserId)
                    .ToListAsync();

                excludedUserIds.Add(userId);

                var globalUsers = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Where(u => !excludedUserIds.Contains(u.Id) &&
                                u.IsActive &&
                                u.IsDiscoverable)
                    .Take(30)
                    .ToListAsync();

                var result = globalUsers.Select(user => MapUserToProfileDTO(user))
                    .OrderBy(x => Guid.NewGuid()) // Random order
                    .ToList();

                await _cacheService.SetAsync(cacheKey, result, CacheKeys.Duration.DiscoveryResults);
                return OperationResult<List<UserProfileDTO>>.Successful(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving global users for user: {UserId}", userId);
                return OperationResult<List<UserProfileDTO>>.Failed("Unable to find users globally. Please try again.");
            }
        }

        private static OperationResult ValidateUpdateProfileRequest(UpdateProfileRequest request)
        {
            if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge >= request.MaxAge)
            {
                return OperationResult.ValidationFailure("Minimum age must be less than maximum age.");
            }

            if (request.HeightCm.HasValue && (request.HeightCm < 120 || request.HeightCm > 250))
            {
                return OperationResult.ValidationFailure("Height must be between 120cm and 250cm.");
            }

            if (!string.IsNullOrEmpty(request.Bio) && request.Bio.Length > 500)
            {
                return OperationResult.ValidationFailure("Bio cannot exceed 500 characters.");
            }

            return OperationResult.Successful();
        }

        private static void UpdateBasicUserProperties(User user, UpdateProfileRequest request)
        {
            if (!string.IsNullOrEmpty(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrEmpty(request.LastName))
                user.LastName = request.LastName;

            if (!string.IsNullOrEmpty(request.Bio))
                user.Bio = request.Bio;

            if (!string.IsNullOrEmpty(request.Occupation))
                user.Occupation = request.Occupation;

            if (!string.IsNullOrEmpty(request.Hometown))
                user.Hometown = request.Hometown;

            if (request.InterestedIn.HasValue)
                user.InterestedIn = request.InterestedIn;

            if (request.Heritage != null)
                user.Heritage = request.Heritage;

            if (request.Religion != null)
                user.Religion = request.Religion;

            if (request.LanguagesSpoken != null)
                user.LanguagesSpoken = request.LanguagesSpoken;

            if (request.EducationLevel.HasValue)
                user.EducationLevel = request.EducationLevel.Value;

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

            if (request.IsGloballyDiscoverable.HasValue)
                user.IsGloballyDiscoverable = request.IsGloballyDiscoverable.Value;

            if (request.IsDiscoverable.HasValue)
                user.IsDiscoverable = request.IsDiscoverable.Value;

            if (request.Children.HasValue)
                user.Children = request.Children.Value;

            if (request.FamilyPlan.HasValue)
                user.FamilyPlan = request.FamilyPlan.Value;

            if (request.Drugs.HasValue)
                user.Drugs = request.Drugs.Value;

            if (request.Smoking.HasValue)
                user.Smoking = request.Smoking.Value;

            if (request.Marijuana.HasValue)
                user.Marijuana = request.Marijuana.Value;

            if (request.Drinking.HasValue)
                user.Drinking = request.Drinking.Value;
        }

        private void UpdateUserPreferences(User user, UpdateProfileRequest request)
        {
            if (user.Preferences == null)
            {
                user.Preferences = new UserPreference
                {
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                };
                _dbContext.UserPreferences.Add(user.Preferences);
            }

            // Update member filter preferences (available to all users)
            if (request.PreferredHeritage != null)
                user.Preferences.PreferredHeritage = request.PreferredHeritage;

            if (request.PreferredReligions != null)
                user.Preferences.PreferredReligions = request.PreferredReligions;

            if (request.LanguagePreference != null)
                user.Preferences.LanguagePreference = request.LanguagePreference;

            // Update subscriber filter preferences (only for premium users)
            var isSubscriber = user.Subscription?.SubscriptionType == SubscriptionType.Premium &&
                              user.Subscription.IsActive &&
                              (!user.Subscription.ExpiresAt.HasValue || user.Subscription.ExpiresAt > DateTime.UtcNow);

            if (isSubscriber)
            {
                if (request.PreferredHeightMin.HasValue)
                    user.Preferences.PreferredHeightMin = request.PreferredHeightMin.Value;

                if (request.PreferredHeightMax.HasValue)
                    user.Preferences.PreferredHeightMax = request.PreferredHeightMax.Value;

                if (request.PreferredChildren != null)
                    user.Preferences.PreferredChildren = request.PreferredChildren;

                if (request.PreferredFamilyPlans != null)
                    user.Preferences.PreferredFamilyPlans = request.PreferredFamilyPlans;

                if (request.PreferredDrugs != null)
                    user.Preferences.PreferredDrugs = request.PreferredDrugs;

                if (request.PreferredSmoking != null)
                    user.Preferences.PreferredSmoking = request.PreferredSmoking;

                if (request.PreferredMarijuana != null)
                    user.Preferences.PreferredMarijuana = request.PreferredMarijuana;

                if (request.PreferredDrinking != null)
                    user.Preferences.PreferredDrinking = request.PreferredDrinking;

                if (request.PreferredEducationLevels != null)
                    user.Preferences.PreferredEducationLevels = request.PreferredEducationLevels;
            }

            user.Preferences.UpdatedAt = DateTime.UtcNow;

        }

        private async Task DeleteUserRelatedData(User user)
        {
            // Delete photos
            _dbContext.UserPhotos.RemoveRange(user.Photos);

            // Delete preferences
            if (user.Preferences != null)
            {
                _dbContext.UserPreferences.Remove(user.Preferences);
            }

            // Delete likes given and received
            _dbContext.UserLikes.RemoveRange(user.LikesGiven);
            _dbContext.UserLikes.RemoveRange(user.LikesReceived);

            // Delete conversations and messages
            var conversations = user.ConversationsAsUser1.Concat(user.ConversationsAsUser2).Distinct();
            foreach (var conversation in conversations)
            {
                var messages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversation.Id)
                    .ToListAsync();
                _dbContext.Messages.RemoveRange(messages);
                _dbContext.Conversations.Remove(conversation);
            }

            // Delete subscription and usage limits
            if (user.Subscription != null)
            {
                _dbContext.UserSubscriptions.Remove(user.Subscription);
            }

            if (user.UsageLimit != null)
            {
                _dbContext.UserUsageLimits.Remove(user.UsageLimit);
            }

            // TODO: Check if user.ReportsMade is working: Delete reports
            //var userReports = _dbContext.UserReports
            //.Where(ur => ur.ReporterId == user.Id);
            //_dbContext.UserReports.RemoveRange(userReports);
            _dbContext.UserReports.RemoveRange(user.ReportsMade);

            // Delete notifications
            _dbContext.PushNotifications.RemoveRange(user.Notifications);

            // Finally, delete the user
            _dbContext.Users.Remove(user);
        }

        private static UserProfileDTO MapUserToProfileDTO(User user, double? distance = null)
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
                Hometown = user.Hometown,
                Children = user.Children,
                FamilyPlan = user.FamilyPlan,
                Drugs = user.Drugs,
                Smoking = user.Smoking,
                Marijuana = user.Marijuana,
                Drinking = user.Drinking,
                DistanceKm = distance,
                EnableGlobalDiscovery = user.EnableGlobalDiscovery,
                IsDiscoverable = user.IsDiscoverable,
                IsGloballyDiscoverable = user.IsGloballyDiscoverable,
                IsVerified = user.IsVerified,
                LastActive = user.LastActive,
                Photos = user.Photos?.Select(p => new UserPhotoDTO
                {
                    Id = p.Id,
                    Url = p.Url,
                    ThumbnailUrl = p.ThumbnailUrl,
                    PhotoOrder = p.PhotoOrder,
                    IsPrimary = p.IsPrimary
                }).OrderBy(p => p.PhotoOrder).ToList() ?? new List<UserPhotoDTO>()
            };
        }

    }
}
