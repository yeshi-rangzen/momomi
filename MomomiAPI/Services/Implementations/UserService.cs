using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly ILogger<UserService> _logger;

        private readonly IMemoryCache _inMemoryActiveUsers;
        private readonly TimeSpan _inMemoryCacheExpiry = TimeSpan.FromMinutes(5);

        public UserService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<UserService> logger,
            Supabase.Client supabaseClient,
            IMemoryCache memoryCache)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
            _supabaseClient = supabaseClient;
            _inMemoryActiveUsers = memoryCache;
        }

        /// <summary>
        /// Gets user profile with optimized caching
        /// </summary>
        public async Task<UserProfileResult> GetUserProfileAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving user profile: {UserId}", userId);

                var cacheKey = CacheKeys.Users.Profile(userId);

                var userDto = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await FetchUserProfileFromDatabase(userId),
                    CacheKeys.Duration.UserProfile
                );

                if (userDto == null)
                {
                    return UserProfileResult.UserNotFound();
                }

                _logger.LogDebug("Retrieved profile for user {UserId}", userId);
                return UserProfileResult.Successful(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile: {UserId}", userId);
                return UserProfileResult.Error("Unable to retrieve user profile. Please try again.");
            }
        }

        public async Task<UserProfileResult> GetUserProfileWithSupabaseIdAsync(Guid supabaseUid)
        {
            try
            {
                _logger.LogInformation("Retrieving user profile with supabaseUid: {UserId}", supabaseUid);

                var user = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Include(u => u.Preferences)
                    .Include(u => u.Subscription)
                    .Include(u => u.UsageLimit)
                    .FirstOrDefaultAsync(u => u.SupabaseUid == supabaseUid && u.IsActive);

                if (user == null)
                {
                    return UserProfileResult.UserNotFound();
                }

                var userDto = UserMapper.UserToDTO(user);

                var cacheKey = CacheKeys.Users.Profile(userDto.Id);
                _ = _cacheService.SetAsync(cacheKey, userDto, CacheKeys.Duration.UserProfile);

                _logger.LogDebug("Retrieved profile for user {UserId}", userDto.Id);
                return UserProfileResult.Successful(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user profile with supabaseId: {UserId}", supabaseUid);
                return UserProfileResult.Error("Unable to retrieve user profile. Please try again.");
            }
        }

        /// <summary>
        /// Updates user profile with optimized cache invalidation
        /// </summary>
        public async Task<ProfileUpdateResult> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            try
            {
                _logger.LogInformation("Updating user profile: {UserId}", userId);

                // Validate request
                var validationResult = ValidateProfileUpdateRequest(request);
                if (!validationResult.Success)
                {
                    return ProfileUpdateResult.ValidationError(validationResult.ErrorMessage!);
                }

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return ProfileUpdateResult.UserNotFound();
                }

                // Track what fields are being updated
                var updatedFields = new List<string>();
                var requiresDiscoveryRefresh = false;

                // Update basic user properties
                UpdateBasicUserProperties(user, request, updatedFields, ref requiresDiscoveryRefresh);

                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Get updated profile
                var userDto = await FetchUserProfileFromDatabase(userId);
                if (userDto == null)
                {
                    return ProfileUpdateResult.Error("Failed to retrieve updated profile");
                }

                _logger.LogInformation("Successfully updated profile for user {UserId}, fields: {Fields}",
                    userId, string.Join(", ", updatedFields));

                // Invalidate relevant caches based on what was updated
                _ = InvalidateProfileCaches(userDto, requiresDiscoveryRefresh);

                return ProfileUpdateResult.Successful(updatedFields);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile: {UserId}", userId);
                return ProfileUpdateResult.Error("Unable to update profile. Please try again.");
            }
        }


        /// <summary>
        /// Updates discovery filters with comprehensive validation and cache optimization
        /// </summary>
        public async Task<DiscoveryFiltersUpdateResult> UpdateDiscoveryFiltersAsync(Guid userId, UpdateDiscoveryFiltersRequest request)
        {
            try
            {
                _logger.LogInformation("Updating discovery filters for user {UserId}", userId);

                // Validate request
                var validationResult = ValidateDiscoveryFiltersRequest(request);
                if (!validationResult.Success)
                {
                    return DiscoveryFiltersUpdateResult.ValidationError(validationResult.ErrorMessage!);
                }

                // Get user with subscription info for premium feature validation
                var user = await _dbContext.Users
                    .Include(u => u.Preferences)
                    .Include(u => u.Subscription)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return DiscoveryFiltersUpdateResult.UserNotFound();
                }

                var isPremiumUser = IsActivePremiumUser(user);
                var updatedFilters = new List<string>();

                // Update discovery settings and preferences
                UpdateDiscoverySettings(user, request, updatedFilters);
                UpdateDiscoveryPreferences(user, request, updatedFilters, isPremiumUser);

                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                var userDto = await FetchUserProfileFromDatabase(userId);

                if (userDto != null)
                {
                    await InvalidateProfileCaches(userDto, true);
                }

                _logger.LogInformation("Successfully updated discovery filters for user {UserId}, filters: {Filters}",
                    userId, string.Join(", ", updatedFilters));

                return DiscoveryFiltersUpdateResult.Successful(updatedFilters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discovery filters for user {UserId}", userId);
                return DiscoveryFiltersUpdateResult.Error("Unable to update discovery filters. Please try again.");
            }
        }

        /// <summary>
        /// Deactivates user account with optimized cache cleanup
        /// </summary>
        public async Task<AccountDeactivationResult> DeactivateUserAccountAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deactivating user: {UserId}", userId);

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return AccountDeactivationResult.UserNotFound();
                }

                var wasActive = user.IsActive;
                if (!wasActive)
                {
                    return AccountDeactivationResult.AlreadyInactive();
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Invalidate all user-related caches
                await InvalidateAllUserCaches(userId);

                _logger.LogInformation("Successfully deactivated user {UserId}", userId);
                return AccountDeactivationResult.Successful(userId, wasActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", userId);
                return AccountDeactivationResult.Error("Unable to deactivate account. Please try again.");
            }
        }

        /// <summary>
        /// Deletes user account and all associated data with optimized batch operations
        /// </summary>
        public async Task<AccountDeletionResult> DeleteUserAccountAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting user account: {UserId}", userId);

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Get user with all related data for counting
                        var user = await _dbContext.Users
                            .Include(u => u.Photos)
                            .Include(u => u.Preferences)
                            .Include(u => u.SwipesGiven)
                            .Include(u => u.SwipesReceived)
                            .Include(u => u.ConversationsAsUser1)
                            .Include(u => u.ConversationsAsUser2)
                            .Include(u => u.MessagesSent)
                            .Include(u => u.Subscription)
                            .Include(u => u.UsageLimit)
                            .Include(u => u.Notifications)
                            .FirstOrDefaultAsync(u => u.Id == userId);

                        if (user == null)
                        {
                            return AccountDeletionResult.UserNotFound();
                        }

                        // Count items for reporting
                        var photosCount = user.Photos.Count;
                        var conversationsCount = user.ConversationsAsUser1.Count + user.ConversationsAsUser2.Count;
                        var swipesCount = user.SwipesGiven.Count + user.SwipesReceived.Count;

                        // Delete user photos from storage (fire and forget for performance)
                        _ = DeleteUserPhotosFromStorage(user.Photos.ToList());

                        // Delete database records in correct order
                        await DeleteUserRelatedDataOptimized(user);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Invalidate all user-related caches
                        _ = InvalidateAllUserCaches(userId);

                        _logger.LogInformation("Successfully deleted user {UserId} and all associated data", userId);
                        return AccountDeletionResult.Successful(userId, photosCount, conversationsCount, swipesCount);
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
                return AccountDeletionResult.Error("Unable to delete account. Please try again.");
            }
        }

        /// <summary>
        /// Highly optimized user activity check with multi-layer caching
        /// Called on every API request for authorization
        /// </summary>
        public async Task<bool?> IsActiveUser(Guid userId)
        {
            try
            {
                // Layer 1: In-memory cache for ultra-fast access (most recent checks)
                if (_inMemoryActiveUsers.TryGetValue(userId, out var cachedResult) && cachedResult != null)
                {
                    // Check if cachedResult is not null AND is the correct type
                    if (cachedResult is bool isActive)
                    {
                        _logger.LogDebug("In-memory cache hit for user activity check: {UserId}", userId);
                        return isActive;
                    }
                    else
                    {
                        // cachedResult is null or wrong type - remove from cache
                        _logger.LogWarning("Invalid cached result for user {UserId}, removing from cache", userId);
                        _inMemoryActiveUsers.Remove(userId);
                    }
                }

                // Layer 2: Redis cache for fast distributed access
                var userResult = await GetUserProfileAsync(userId);
                if (userResult != null && userResult.Data != null)
                {
                    _logger.LogDebug("Redis cache hit for user activity check: {UserId}", userId);

                    var cacheEntryOptionsFromRedis = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _inMemoryCacheExpiry,
                        Priority = CacheItemPriority.Normal,
                        Size = 1 // Each entry counts as size 1
                    };
                    // Update in-memory cache for next time
                    _inMemoryActiveUsers.Set(userId, true, cacheEntryOptionsFromRedis);
                    return true;
                }
                else
                {
                    _logger.LogWarning("User not found or inactive during activity check: {UserId}", userId);
                    _inMemoryActiveUsers.Remove(userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user activity status: {UserId}", userId);

                // Fallback: assume user is active to avoid blocking valid requests
                // Security note: This is a availability vs security trade-off
                // Alternative: return false for fail-secure behavior
                return false;
            }
        }

        #region Private Helper Methods


        /// <summary>
        /// Fetches complete user profile from database
        /// </summary>
        private async Task<UserDTO?> FetchUserProfileFromDatabase(Guid userId)
        {
            var user = await _dbContext.Users
                .Include(u => u.Photos.OrderBy(p => p.PhotoOrder))
                .Include(u => u.Preferences)
                .Include(u => u.Subscription)
                .Include(u => u.UsageLimit)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user?.UsageLimit != null)
            {
                bool limitsReset = ResetDailyLimitsIfNeeded(user.UsageLimit);

                if (limitsReset)
                {
                    // Persist changes to the database
                    await _dbContext.SaveChangesAsync();
                }
            }

            return user != null ? UserMapper.UserToDTO(user) : null;
        }

        private static bool ResetDailyLimitsIfNeeded(UserUsageLimit usageLimit)
        {
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);

            if (usageLimit.LastResetDate.Date < twentyFourHoursAgo)
            {
                usageLimit.LikesUsedToday = 0;
                usageLimit.SuperLikesUsedToday = 0;
                usageLimit.AdsWatchedToday = 0;
                usageLimit.LastResetDate = DateTime.UtcNow.Date;
                return true;
            }
            return false;
        }


        /// <summary>
        /// Validates profile update request
        /// </summary>
        private static OperationResult ValidateProfileUpdateRequest(UpdateProfileRequest request)
        {
            if (!string.IsNullOrEmpty(request.Bio) && request.Bio.Length > 500)
            {
                return OperationResult.ValidationFailure("Bio cannot exceed 500 characters");
            }

            if (request.HeightCm.HasValue && (request.HeightCm < 120 || request.HeightCm > 250))
            {
                return OperationResult.ValidationFailure("Height must be between 120cm and 250cm");
            }

            return OperationResult.Successful();
        }

        /// <summary>
        /// Validates discovery filters request
        /// </summary>
        private static OperationResult ValidateDiscoveryFiltersRequest(UpdateDiscoveryFiltersRequest request)
        {
            if (!request.IsValidAgeRange())
            {
                return OperationResult.ValidationFailure("Minimum age must be less than maximum age");
            }

            if (!request.IsValidHeightRange())
            {
                return OperationResult.ValidationFailure("Minimum height must be less than maximum height");
            }

            if (!request.IsValidLocation())
            {
                return OperationResult.ValidationFailure("Invalid location coordinates. Both latitude and longitude must be provided together");
            }

            if (request.MinAge.HasValue && request.MinAge < 18)
            {
                return OperationResult.ValidationFailure("Minimum age must be at least 18");
            }

            if (request.MaxAge.HasValue && request.MaxAge > 100)
            {
                return OperationResult.ValidationFailure("Maximum age cannot exceed 100");
            }

            if (request.MaxDistanceKm.HasValue && (request.MaxDistanceKm < 1 || request.MaxDistanceKm > 200))
            {
                return OperationResult.ValidationFailure("Maximum distance must be between 1 and 200 km");
            }

            return OperationResult.Successful();
        }

        /// <summary>
        /// Updates basic user properties and tracks changes
        /// </summary>
        private static void UpdateBasicUserProperties(User user, UpdateProfileRequest request,
            List<string> updatedFields, ref bool requiresDiscoveryRefresh)
        {
            if (!string.IsNullOrEmpty(request.FirstName))
            {
                user.FirstName = request.FirstName;
                updatedFields.Add("FirstName");
            }

            if (request.LastName != null)
            {
                user.LastName = request.LastName;
                updatedFields.Add("LastName");
            }

            if (request.Gender.HasValue)
            {
                user.Gender = request.Gender.Value;
                updatedFields.Add("Gender");
            }

            if (request.DateOfBirth.HasValue)
            {
                user.DateOfBirth = request.DateOfBirth.Value;
                updatedFields.Add("Gender");
                requiresDiscoveryRefresh = true;
            }

            if (request.Bio != null)
            {
                user.Bio = request.Bio;
                updatedFields.Add("Bio");
            }

            if (request.Hometown != null)
            {
                user.Hometown = request.Hometown;
                updatedFields.Add("Hometown");
            }

            if (request.Occupation != null)
            {
                user.Occupation = request.Occupation;
                updatedFields.Add("Occupation");
            }

            if (request.HeightCm.HasValue)
            {
                user.HeightCm = request.HeightCm.Value;
                updatedFields.Add("HeightCm");
            }

            if (request.Heritage != null)
            {
                user.Heritage = request.Heritage;
                updatedFields.Add("Heritage");
            }

            if (request.Religion != null)
            {
                user.Religion = request.Religion;
                updatedFields.Add("Religion");
            }

            if (request.LanguagesSpoken != null)
            {
                user.LanguagesSpoken = request.LanguagesSpoken;
                updatedFields.Add("LanguagesSpoken");
            }

            if (request.EducationLevel.HasValue)
            {
                user.EducationLevel = request.EducationLevel.Value;
                updatedFields.Add("EducationLevel");
            }

            if (request.Children.HasValue)
            {
                user.Children = request.Children.Value;
                updatedFields.Add("Children");
            }

            if (request.FamilyPlan.HasValue)
            {
                user.FamilyPlan = request.FamilyPlan.Value;
                updatedFields.Add("FamilyPlan");
            }

            if (request.Drugs.HasValue)
            {
                user.Drugs = request.Drugs.Value;
                updatedFields.Add("Drugs");
            }

            if (request.Smoking.HasValue)
            {
                user.Smoking = request.Smoking.Value;
                updatedFields.Add("Smoking");
            }

            if (request.Marijuana.HasValue)
            {
                user.Marijuana = request.Marijuana.Value;
                updatedFields.Add("Marijuana");
            }

            if (request.Drinking.HasValue)
            {
                user.Drinking = request.Drinking.Value;
                updatedFields.Add("Drinking");
            }

            if (request.NotificationsEnabled.HasValue)
            {
                user.NotificationsEnabled = request.NotificationsEnabled.Value;
                updatedFields.Add("NotificationsEnabled");
            }

            if (request.PushToken != null)
            {
                user.PushToken = request.PushToken;
                updatedFields.Add("PushToken");
            }
            if (request.IsOnboarding.HasValue)
            {
                user.IsOnboarding = request.IsOnboarding.Value;
                updatedFields.Add("IsOnboarding");
            }
        }

        /// <summary>
        /// Updates discovery settings and tracks changes
        /// </summary>
        private static void UpdateDiscoverySettings(User user, UpdateDiscoveryFiltersRequest request,
            List<string> updatedFilters)
        {
            if (request.IsDiscoverable.HasValue)
            {
                user.IsDiscoverable = request.IsDiscoverable.Value;
                updatedFilters.Add("IsDiscoverable");
            }

            if (request.IsGloballyDiscoverable.HasValue)
            {
                user.IsGloballyDiscoverable = request.IsGloballyDiscoverable.Value;
                updatedFilters.Add("IsGloballyDiscoverable");
            }

            if (request.EnableGlobalDiscovery.HasValue)
            {
                user.EnableGlobalDiscovery = request.EnableGlobalDiscovery.Value;
                updatedFilters.Add("EnableGlobalDiscovery");
            }

            if (request.InterestedIn.HasValue)
            {
                user.InterestedIn = request.InterestedIn.Value;
                updatedFilters.Add("InterestedIn");
            }

            if (request.MinAge.HasValue)
            {
                user.MinAge = request.MinAge.Value;
                updatedFilters.Add("MinAge");
            }

            if (request.MaxAge.HasValue)
            {
                user.MaxAge = request.MaxAge.Value;
                updatedFilters.Add("MaxAge");
            }

            if (request.MaxDistanceKm.HasValue)
            {
                user.MaxDistanceKm = request.MaxDistanceKm.Value;
                updatedFilters.Add("MaxDistanceKm");
            }

            // Location updates
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                var oldLat = user.Latitude;
                var oldLon = user.Longitude;

                user.Latitude = (decimal)request.Latitude.Value;
                user.Longitude = (decimal)request.Longitude.Value;

                // Check if location changed significantly (>1km)
                if (oldLat != 0 && oldLon != 0)
                {
                    var distance = LocationHelper.CalculateDistance(
                        oldLat, oldLon,
                        (decimal)request.Latitude.Value, (decimal)request.Longitude.Value);
                }

                updatedFilters.Add("Location");
            }

            if (request.Neighbourhood != null)
            {
                user.Neighbourhood = request.Neighbourhood;
                updatedFilters.Add("Neighbourhood");
            }
        }

        /// <summary>
        /// Updates discovery preferences and tracks changes
        /// </summary>
        private void UpdateDiscoveryPreferences(User user, UpdateDiscoveryFiltersRequest request,
            List<string> updatedFilters, bool isPremiumUser)
        {
            // Ensure preferences exist
            if (user.Preferences == null)
            {
                user.Preferences = new UserPreference
                {
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                };
                _dbContext.UserPreferences.Add(user.Preferences);
            }

            // Free user filters
            if (request.PreferredHeritage != null)
            {
                user.Preferences.PreferredHeritage = request.PreferredHeritage;
                updatedFilters.Add("PreferredHeritage");
            }

            if (request.PreferredReligions != null)
            {
                user.Preferences.PreferredReligions = request.PreferredReligions;
                updatedFilters.Add("PreferredReligions");
            }


            // Premium user filters (only if user has premium subscription)
            if (isPremiumUser)
            {
                if (request.PreferredLanguagesSpoken != null)
                {
                    user.Preferences.LanguagePreference = request.PreferredLanguagesSpoken;
                    updatedFilters.Add("PreferredLanguagesSpoken");
                }

                if (request.PreferredHeightMin.HasValue)
                {
                    user.Preferences.PreferredHeightMin = request.PreferredHeightMin.Value;
                    updatedFilters.Add("PreferredHeightMin");
                }

                if (request.PreferredHeightMax.HasValue)
                {
                    user.Preferences.PreferredHeightMax = request.PreferredHeightMax.Value;
                    updatedFilters.Add("PreferredHeightMax");
                }

                if (request.PreferredEducationLevels != null)
                {
                    user.Preferences.PreferredEducationLevels = request.PreferredEducationLevels;
                    updatedFilters.Add("PreferredEducationLevels");
                }

                if (request.PreferredChildren != null)
                {
                    user.Preferences.PreferredChildren = request.PreferredChildren;
                    updatedFilters.Add("PreferredChildren");
                }

                if (request.PreferredFamilyPlans != null)
                {
                    user.Preferences.PreferredFamilyPlans = request.PreferredFamilyPlans;
                    updatedFilters.Add("PreferredFamilyPlans");
                }

                if (request.PreferredDrugs != null)
                {
                    user.Preferences.PreferredDrugs = request.PreferredDrugs;
                    updatedFilters.Add("PreferredDrugs");
                }

                if (request.PreferredSmoking != null)
                {
                    user.Preferences.PreferredSmoking = request.PreferredSmoking;
                    updatedFilters.Add("PreferredSmoking");
                }

                if (request.PreferredDrinking != null)
                {
                    user.Preferences.PreferredDrinking = request.PreferredDrinking;
                    updatedFilters.Add("PreferredDrinking");
                }

                if (request.PreferredMarijuana != null)
                {
                    user.Preferences.PreferredMarijuana = request.PreferredMarijuana;
                    updatedFilters.Add("PreferredMarijuana");
                }
            }

            user.Preferences.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Checks if user has active premium subscription
        /// </summary>
        private static bool IsActivePremiumUser(User user)
        {
            return user.Subscription?.SubscriptionType == SubscriptionType.Premium &&
                   (!user.Subscription.ExpiresAt.HasValue || user.Subscription.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>
        /// Invalidates profile-related caches based on updated fields
        /// </summary>
        private async Task InvalidateProfileCaches(UserDTO userDto, bool requiresDiscoveryRefresh)
        {
            // Set updated profile in cache
            await _cacheService.SetAsync(CacheKeys.Users.Profile(userDto.Id), userDto, CacheKeys.Duration.UserProfile);

            //if (requiresDiscoveryRefresh)
            //{
            //    var keysToInvalidate = new List<string>
            //    {
            //        CacheKeys.Discovery.GlobalResults(userDto.Id),
            //        CacheKeys.Discovery.LocalResults(userDto.Id)
            //    };

            //    await _cacheService.RemoveManyAsync(keysToInvalidate);
            //}

            _logger.LogDebug("Invalidated cache keys for user {UserId}", userDto.Id);
        }


        /// <summary>
        /// Invalidates all user-related caches
        /// </summary>
        private async Task InvalidateAllUserCaches(Guid userId)
        {
            var keysToInvalidate = new List<string>
            {
                CacheKeys.Users.Profile(userId),
                CacheKeys.Matching.UserMatches(userId),
                CacheKeys.Messaging.UserConversations(userId),
                CacheKeys.Discovery.GlobalResults(userId),
                CacheKeys.Discovery.LocalResults(userId),
            };

            await _cacheService.RemoveManyAsync(keysToInvalidate);
            _logger.LogDebug("Invalidated all caches for user {UserId}", userId);
        }

        /// <summary>
        /// Deletes user photos from Supabase storage
        /// </summary>
        private async Task DeleteUserPhotosFromStorage(List<UserPhoto> photos)
        {
            try
            {
                if (!photos.Any()) return;

                var filePaths = photos.Select(p => p.StoragePath).ToList();
                await _supabaseClient.Storage
                    .From("user-photos")
                    .Remove(filePaths);

                _logger.LogInformation("Deleted {Count} photos from storage", photos.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete some photos from storage");
            }
        }

        /// <summary>
        /// Optimized deletion of user-related data
        /// </summary>
        private async Task DeleteUserRelatedDataOptimized(User user)
        {
            // Delete photos
            if (user.Photos.Any())
            {
                _dbContext.UserPhotos.RemoveRange(user.Photos);
            }

            // Delete preferences
            if (user.Preferences != null)
            {
                _dbContext.UserPreferences.Remove(user.Preferences);
            }

            // Delete swipes (batch operation)
            var allSwipes = user.SwipesGiven.Concat(user.SwipesReceived).Distinct();
            if (allSwipes.Any())
            {
                _dbContext.UserSwipes.RemoveRange(allSwipes);
            }

            // Delete conversations and messages
            var conversations = user.ConversationsAsUser1.Concat(user.ConversationsAsUser2).Distinct();
            foreach (var conversation in conversations)
            {
                var messages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversation.Id)
                    .ToListAsync();

                if (messages.Any())
                {
                    _dbContext.Messages.RemoveRange(messages);
                }
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

            // Delete notifications
            if (user.Notifications.Any())
            {
                _dbContext.PushNotifications.RemoveRange(user.Notifications);
            }

            // Finally, delete the user
            _dbContext.Users.Remove(user);
        }


        #endregion
    }
}