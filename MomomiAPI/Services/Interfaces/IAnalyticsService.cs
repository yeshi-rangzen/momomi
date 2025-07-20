using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IAnalyticsService
    {
        // User Registration & Authentication
        Task TrackUserRegistrationAsync(Guid userId, UserRegistrationData data);
        Task TrackEmailVerificationSentAsync(string email, int attemptCount);
        Task TrackUserLoginAsync(Guid userId, LoginData data);
        Task TrackDailyActivityAsync(Guid userId);

        // Discovery & Matching
        Task TrackLikeRecordedAsync(Guid userId, Guid likedUserId, LikeInteractionData data);
        Task TrackMatchCreatedAsync(Guid userId, Guid matchedUserId, MatchData data);
        Task TrackMessageDeliveredAsync(Guid senderId, Guid receiverId, MessageData data);

        // Subscription & Matching
        Task TrackSubscriptionUpgradedAsync(Guid userId, SubscriptionData data);
        Task TrackSubscriptionCancelledAsync(Guid userId, CancellationData data);

        // Retention & Performance
        Task TrackRetentionMilestoneAsync(Guid userId, RetentionData data);
        Task TrackApiPerformanceAsync(string endpoint, TimeSpan responseTime, int statusCode, Guid? userId = null);

        // Health & Diagnostics
        Task<bool> IsHealthyAsync();
    }

    // Analytics Data Models
    public class UserRegistrationData
    {
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public GenderType Gender { get; set; }
        public List<HeritageType> Heritage { get; set; } = new();
        public List<ReligionType> Religion { get; set; } = new();
        public List<LanguageType> Languages { get; set; } = new();
        public string? Hometown { get; set; }
        public string RegistrationMethod { get; set; } = "email";
        public DateTime RegistrationTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class LoginData
    {
        public string Email { get; set; } = string.Empty;
        public string LoginMethod { get; set; } = "email_otp";
        public int DaysSinceLastLogin { get; set; }
        public DateTime LoginTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class LikeInteractionData
    {
        public LikeType LikeType { get; set; }
        public string DiscoveryMode { get; set; } = "unknown";
        public double? CulturalCompatibilityScore { get; set; }
        public List<HeritageType>? TargetHeritage { get; set; }
        public bool IsMatch { get; set; }
        public DateTime InteractionTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class MatchData
    {
        public string MatchType { get; set; } = "regular";
        public double CulturalCompatibilityScore { get; set; }
        public List<HeritageType> User1Heritage { get; set; } = new();
        public List<HeritageType> User2Heritage { get; set; } = new();
        public bool IsCrossCultural { get; set; }
        public DateTime MatchTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class MessageData
    {
        public Guid ConversationId { get; set; }
        public string MessageType { get; set; } = "text";
        public int MessageLength { get; set; }
        public bool IsFirstMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime MessageTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class SubscriptionData
    {
        public SubscriptionType PlanType { get; set; }
        public int DurationMonths { get; set; }
        public decimal PricePaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string TriggerReason { get; set; } = string.Empty;
        public SubscriptionType PreviousSubscription { get; set; }
        public DateTime UpgradeTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class CancellationData
    {
        public string CancellationReason { get; set; } = string.Empty;
        public int DaysSubscribed { get; set; }
        public SubscriptionType CancelledPlan { get; set; }
        public DateTime CancellationTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class RetentionData
    {
        public string MilestoneType { get; set; } = string.Empty; // "day_1", "day_7", "day_30"
        public DateTime RegistrationDate { get; set; }
        public DateTime FirstReturnDate { get; set; }
        public int TotalSessionsInPeriod { get; set; }
        public int DaysSinceRegistration { get; set; }
    }
}
