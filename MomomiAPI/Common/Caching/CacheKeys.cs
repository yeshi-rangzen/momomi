namespace MomomiAPI.Common.Caching
{
    public static class CacheKeys
    {
        // Cache durations
        public static class Duration
        {
            public static readonly TimeSpan RefreshToken = TimeSpan.FromDays(30);
            public static readonly TimeSpan OtpAttempt = TimeSpan.FromMinutes(10);
            public static readonly TimeSpan OtpRateLimit = TimeSpan.FromHours(1);
            public static readonly TimeSpan EmailVerificationCode = TimeSpan.FromMinutes(30);
            public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
            public static readonly TimeSpan UserTokenRevocation = TimeSpan.FromDays(7);

            public static readonly TimeSpan UserMatches = TimeSpan.FromMinutes(15);
            public static readonly TimeSpan UserProfile = TimeSpan.FromMinutes(30);
            public static readonly TimeSpan DiscoveryResults = TimeSpan.FromMinutes(15);
            public static readonly TimeSpan Conversations = TimeSpan.FromMinutes(10);
            public static readonly TimeSpan UsageLimits = TimeSpan.FromMinutes(5);
            public static readonly TimeSpan TokenBlacklist = TimeSpan.FromHours(24);
            public static readonly TimeSpan UserOnlineStatus = TimeSpan.FromMinutes(5);
            public static readonly TimeSpan BlockedUsers = TimeSpan.FromMinutes(15);
        }
        public static class Authentication
        {
            public static string OtpAttempt(string email) => $"auth:otp:{email.ToLowerInvariant()}";
            public static string OtpRateLimit(string email) => $"auth:rate_limit:{email.ToLowerInvariant()}";
            public static string EmailVerificationToken(string email, string token) => $"auth:email_verification_token:{email.ToLowerInvariant()}:{token}";
            public static string RefreshToken(Guid userId) => $"auth:refresh_token:{userId}";
            public static string TokenToUser(string refreshToken) => $"auth:token_to_user:{refreshToken}";
            public static string BlacklistedToken(string jti) => $"auth:blacklist_token:{jti}";
            public static string UserTokenRevocation(Guid userId) => $"auth:user_token_revocation:{userId}";
        }


        // User-related cache keys
        public static class Users
        {
            public static string Profile(Guid userId) => $"user:profile:{userId}";
            public static string Photos(Guid userId) => $"user:photos:{userId}";
            public static string Preferences(Guid userId) => $"user:preferences:{userId}";
            public static string SubscriptionStatus(Guid userId) => $"user:subscription:{userId}";
            public static string UsageLimits(Guid userId) => $"user:usage:{userId}";
            public static string OnlineStatus(Guid userId) => $"user:online:{userId}";
        }

        // Discovery and matching cache keys
        public static class Discovery
        {
            public static string Results(Guid userId, string discoveryMode)
                => $"discovery:users:{userId}:{discoveryMode}";
            public static string GlobalResults(Guid userId)
                => Results(userId, "global");
            public static string LocalResults(Guid userId)
                => Results(userId, "local");
        }

        public static class Matching
        {
            public static string UserMatches(Guid userId) => $"matches:user:{userId}";

        }

        // Authentication cache keys

        // Messaging cache keys
        public static class Messaging
        {
            public static string ConversationCount(Guid conversationId) => $"messages:conversation_count:{conversationId}";
            public static string UserOnline(Guid userId) => $"messages:user_online:{userId}";
            public static string UserConversations(Guid userId) => $"messages:conversations:{userId}";
            public static string ConversationMessages(Guid conversationId, int page) => $"messages:conversation:{conversationId}:page:{page}";
            public static string ConversationDetails(Guid conversationId) => $"messages:details:{conversationId}";
        }

        // Notification cache keys
        public static class Notifications
        {
            public static string UserNotifications(Guid userId, int page) => $"notifications:user:{userId}:page:{page}";
            public static string UnreadCount(Guid userId) => $"notifications:unread:{userId}";
        }

        // Blocking and reporting cache keys
        public static class Safety
        {
            public static string BlockedUsers(Guid userId) => $"safety:blocked:{userId}";
            public static string UserReports(Guid userId) => $"safety:reports:{userId}";
            public static string BlockStatus(Guid user1Id, Guid user2Id)
            {
                var (smaller, larger) = user1Id.CompareTo(user2Id) < 0
                    ? (user1Id, user2Id)
                    : (user2Id, user1Id);
                return $"safety:block_status:{smaller}:{larger}";
            }
        }
    }
}
