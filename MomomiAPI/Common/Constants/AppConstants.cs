namespace MomomiAPI.Common.Constants
{
    public static class AppConstants
    {
        public static class Limits
        {
            public const int FreeUserDailyLikes = 10;
            public const int FreeUserDailySuperLikes = 1;
            public const int PremiumUserDailyLikes = 50;
            public const int PremiumUserDailySuperLikes = 10;
            public const int FreeUserDailyAds = 3;
            public const int MaxPhotosPerUser = 6;
            public const int MinPhotosRequired = 3;
            public const int MaxConversationMessagesPerPage = 50;
            public const int MaxNotificationsPerPage = 20;
            public const int MaxDiscoveryUsers = 30;
            public const int MaxDistance = 200;
            public const int MinAge = 18;
            public const int MaxAge = 65;
            public const int OtpLength = 6;
            public const int MaxOtpAttempts = 3;
            public const int MaxOtpRequestsPerHour = 3;
            public static readonly TimeSpan MessageDeletionTimeLimit = TimeSpan.FromHours(1);
        }

        public static class FileSizes
        {
            // Updated to match React Native component dimensions (4:5 ratio)
            public const int DisplayPhotoWidth = 1080;
            public const int DisplayPhotoHeight = 1350; // 4:5 ratio (1080x1350)

            // Keep thumbnail smaller for performance
            public const int ThumbnailSize = 300; // Square thumbnail

            // Increased max file size since mobile pre-compresses at 70% quality
            public const long MaxPhotoSizeBytes = 8 * 1024 * 1024; // 8MB (was probably lower before)

            // For mobile optimization - smaller web preview sizes
            public const int WebPreviewWidth = 540;  // Half resolution for web
            public const int WebPreviewHeight = 675; // Maintains 4:5 ratio

            // JPEG quality settings to match mobile
            public const int HighQualityJpeg = 85;    // For display photos
            public const int MediumQualityJpeg = 70;  // Matches mobile compression
            public const int ThumbnailQualityJpeg = 60; // For thumbnails
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
            public const string DefaultPhotoUrl = "https://jcosjsmsespqkixaqgkm.supabase.co/storage/v1/object/public/user-photos/default-avatar.jpg";
            public const int DefaultMaxDistance = 50;
            public const int DefaultMinAge = 18;
            public const int DefaultMaxAge = 35;
            public const bool DefaultGlobalDiscovery = true;
            public const bool DefaultIsDiscoverable = true;
            public const bool DefaultIsGloballyDiscoverable = true;
            public const bool DefaultNotificationsEnabled = false;
        }

        public static class ErrorCodes
        {
            // Authentication & Authorization
            public const string UNAUTHORIZED = "UNAUTHORIZED";
            public const string FORBIDDEN = "FORBIDDEN";
            public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
            public const string OTP_TOKEN_EXPIRED = "OTP_TOKEN_EXPIRED";
            public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";

            // Validation Errors
            public const string VALIDATION_ERROR = "VALIDATION_ERROR";
            public const string INVALID_INPUT = "INVALID_INPUT";
            public const string REQUIRED_FIELD_MISSING = "REQUIRED_FIELD_MISSING";

            // Business Logic Errors
            public const string BUSINESS_RULE_VIOLATION = "BUSINESS_RULE_VIOLATION";
            public const string OPERATION_NOT_ALLOWED = "OPERATION_NOT_ALLOWED";

            // Resource Errors
            public const string NOT_FOUND = "NOT_FOUND";
            public const string USER_NOT_FOUND = "USER_NOT_FOUND";
            public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";

            // Swipe-Specific Errors
            public const string USER_ALREADY_PROCESSED = "USER_ALREADY_PROCESSED";
            public const string USER_BLOCKED = "USER_BLOCKED";
            public const string LIKE_LIMIT_REACHED = "LIKE_LIMIT_REACHED";
            public const string SUPER_LIKE_LIMIT_REACHED = "SUPER_LIKE_LIMIT_REACHED";
            public const string NO_RECENT_PASS_TO_UNDO = "NO_RECENT_PASS_TO_UNDO";

            // System Errors
            public const string INTERNAL_SERVER_ERROR = "INTERNAL_SERVER_ERROR";
            public const string DATABASE_ERROR = "DATABASE_ERROR";
            public const string EXTERNAL_SERVICE_ERROR = "EXTERNAL_SERVICE_ERROR";
            public const string RATE_LIMIT_EXCEEDED = "RATE_LIMIT_EXCEEDED";
        }
    }
}
