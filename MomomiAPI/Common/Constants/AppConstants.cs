namespace MomomiAPI.Common.Constants
{
    public static class AppConstants
    {
        public static class Limits
        {
            public const int FreeUserDailyLikes = 10;
            public const int PremiumUserDailyLikes = int.MaxValue;
            public const int FreeUserWeeklySuperLikes = 1;
            public const int PremiumUserDailySuperLikes = 10;
            public const int FreeUserDailyAds = 10;
            public const int MaxPhotosPerUser = 6;
            public const int MaxConversationMessagesPerPage = 50;
            public const int MaxNotificationsPerPage = 20;
            public const int MaxDiscoveryUsers = 30;
            public const int MaxDistance = 200;
            public const int MinAge = 18;
            public const int MaxAge = 65;
            public const int OtpLength = 6;
            public const int MaxOtpAttempts = 3;
            public const int MaxOtpRequestsPerHour = 3;
        }

        public static class FileSizes
        {
            public const long MaxPhotoSizeBytes = 5 * 1024 * 1024; // 5MB
            public const int MaxPhotoWidthPixels = 2048;
            public const int MaxPhotoHeightPixels = 2048;
            public const int ThumbnailSize = 300;
            public const int DisplayPhotoSize = 800;
        }

        public static class Validation
        {
            public const int MinBioLength = 10;
            public const int MaxBioLength = 500;
            public const int MinNameLength = 1;
            public const int MaxNameLength = 100;
            public const int MaxMessageLength = 1000;
            public const int MaxReportDescriptionLength = 1000;
        }

        public static class DefaultValues
        {
            public const string DefaultPhotoUrl = "https://res.cloudinary.com/dgfyvwnuo/image/upload/v1/momomi/default-avatar.jpg";
            public const int DefaultMaxDistance = 50;
            public const int DefaultMinAge = 18;
            public const int DefaultMaxAge = 35;
            public const bool DefaultGlobalDiscovery = true;
            public const bool DefaultIsDiscoverable = true;
            public const bool DefaultNotificationsEnabled = true;
        }
    }
}
