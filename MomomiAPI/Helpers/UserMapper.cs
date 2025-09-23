using MomomiAPI.Common.Constants;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Helpers
{
    public class UserMapper
    {
        public static UserDTO UserToDTO(User user)
        {
            var userDTO = new UserDTO
            {
                // Identity properties
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,

                // Personal Information
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth, // Assuming DateOfBirth is non-nullable
                Gender = user.Gender,
                HeightCm = user.HeightCm,
                Bio = user.Bio,

                // Location Information
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                Hometown = user.Hometown,
                Neighbourhood = user.Neighbourhood,

                // Cultural & Background Information
                EducationLevel = user.EducationLevel,
                Occupation = user.Occupation,
                Heritage = user.Heritage,
                Religion = user.Religion,
                LanguagesSpoken = user.LanguagesSpoken,

                // Family Plans
                FamilyPlan = user.FamilyPlan,
                Children = user.Children,

                // Vices
                Drugs = user.Drugs,
                Smoking = user.Smoking,
                Drinking = user.Drinking,
                Marijuana = user.Marijuana,

                // Matching & Discovery Preferences
                InterestedIn = user.InterestedIn,
                MinAge = user.MinAge,
                MaxAge = user.MaxAge,
                MaxDistanceKm = user.MaxDistanceKm,
                IsDiscoverable = user.IsDiscoverable,
                IsGloballyDiscoverable = user.IsGloballyDiscoverable,
                EnableGlobalDiscovery = user.EnableGlobalDiscovery,

                // Notifications & Device
                PushToken = user.PushToken,
                NotificationsEnabled = user.NotificationsEnabled,

                // Account status & Verification
                IsVerified = user.IsVerified,
                IsActive = user.IsActive,
                IsOnboarding = user.IsOnboarding,

                // Timestamps
                LastActive = user.LastActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,

                // Related entities
                Photos = user.Photos?.Select(photo => new UserPhotoDTO
                {
                    Id = photo.Id,
                    Url = photo.Url,
                    IsPrimary = photo.IsPrimary,
                    PhotoOrder = photo.PhotoOrder,
                    ThumbnailUrl = photo.ThumbnailUrl,
                }).ToList() ?? [],

                Preferences = user.Preferences != null ? new PreferencesDTO
                {
                    PreferredHeritage = user.Preferences.PreferredHeritage ?? [],
                    PreferredReligions = user.Preferences.PreferredReligions ?? [],
                    LanguagePreference = user.Preferences.LanguagePreference ?? [],

                    PreferredHeightMin = user.Preferences.PreferredHeightMin,
                    PreferredHeightMax = user.Preferences.PreferredHeightMax,
                    PreferredEducationLevels = user.Preferences.PreferredEducationLevels ?? [],
                    PreferredFamilyPlans = user.Preferences.PreferredFamilyPlans ?? [],
                    PreferredChildren = user.Preferences.PreferredChildren ?? [],
                    PreferredDrinking = user.Preferences.PreferredDrinking ?? [],
                    PreferredDrugs = user.Preferences.PreferredDrugs ?? [],
                    PreferredMarijuana = user.Preferences.PreferredMarijuana ?? [],
                    PreferredSmoking = user.Preferences.PreferredSmoking ?? [],
                    CreatedAt = user.Preferences.CreatedAt,
                    UpdatedAt = user.Preferences.UpdatedAt
                } : new PreferencesDTO(),

                Subscription = user.Subscription != null ? new SubscriptionDTO
                {
                    SubscriptionType = user.Subscription.SubscriptionType,
                    ExpiresAt = user.Subscription.ExpiresAt,
                    StartsAt = user.Subscription.StartsAt,
                } : null,
                UsageLimit = user.UsageLimit != null ? new UsageLimitsDTO
                {
                    MaxLikesPerDay = user.Subscription?.SubscriptionType == SubscriptionType.Free ? AppConstants.Limits.FreeUserDailyLikes : AppConstants.Limits.PremiumUserDailyLikes,
                    MaxSuperLikesPerDay = user.Subscription?.SubscriptionType == SubscriptionType.Free ? AppConstants.Limits.FreeUserDailySuperLikes : AppConstants.Limits.PremiumUserDailySuperLikes,
                    LikesUsedToday = user.UsageLimit.LikesUsedToday,
                    SuperLikesUsedToday = user.UsageLimit.SuperLikesUsedToday,
                    AdsWatchedToday = user.UsageLimit.AdsWatchedToday,
                    MaxAdsPerDay = user.Subscription?.SubscriptionType == SubscriptionType.Free ? AppConstants.Limits.FreeUserDailyAds : 0,
                } : null
            };

            return userDTO;
        }

        public static DiscoveryUserDTO UserToDiscoveryDTO(User user)
        {
            var discoveryUserDTO = new DiscoveryUserDTO
            {
                // Identity properties
                Id = user.Id,

                // Personal Information
                FirstName = user.FirstName,
                LastName = user.LastName,
                DateOfBirth = user.DateOfBirth, // Assuming DateOfBirth is non-nullable
                Gender = user.Gender,
                HeightCm = user.HeightCm,
                Bio = user.Bio,

                // Location Information
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                Hometown = user.Hometown,
                Neighbourhood = user.Neighbourhood,

                // Cultural & Background Information
                EducationLevel = user.EducationLevel,
                Occupation = user.Occupation,
                Heritage = user.Heritage,
                Religion = user.Religion,
                LanguagesSpoken = user.LanguagesSpoken,

                // Family Plans
                FamilyPlan = user.FamilyPlan,
                Children = user.Children,

                // Vices
                Drugs = user.Drugs,
                Smoking = user.Smoking,
                Drinking = user.Drinking,
                Marijuana = user.Marijuana,


                // Account status & Verification
                IsVerified = user.IsVerified,

                // Related entities
                Photos = user.Photos?.Select(photo => new UserPhotoDTO
                {
                    Id = photo.Id,
                    Url = photo.Url,
                    IsPrimary = photo.IsPrimary,
                    PhotoOrder = photo.PhotoOrder,
                    ThumbnailUrl = photo.ThumbnailUrl,
                }).ToList() ?? [],

            };

            return discoveryUserDTO;
        }

    }
}
