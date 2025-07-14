using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Common.Results
{
    public enum LikeOutcome
    {
        LikeRecorded,
        MatchCreated,
        LimitReached,
        UserAlreadyProcessed,
        UserBlocked,
        UserNotFound
    }

    public class LikeResult : OperationResult
    {
        public LikeOutcome Outcome { get; private set; }
        public UsageLimitsDTO? UpdatedLimits { get; private set; }
        public bool IsMatch { get; private set; }

        private LikeResult(bool success, LikeOutcome outcome, string? errorMessage, UsageLimitsDTO? updatedLimits, bool isMatch)
            : base(success, errorMessage)
        {
            Outcome = outcome;
            UpdatedLimits = updatedLimits;
            IsMatch = isMatch;
        }

        public static LikeResult MatchCreated(UsageLimitsDTO updatedLimits)
            => new(true, LikeOutcome.MatchCreated, null, updatedLimits, true);

        public static LikeResult LikeRecorded(UsageLimitsDTO updatedLimits)
            => new(true, LikeOutcome.LikeRecorded, null, updatedLimits, false);

        public static LikeResult LimitReached(string message, UsageLimitsDTO currentLimits)
            => new(false, LikeOutcome.LimitReached, message, currentLimits, false);

        public static LikeResult UserAlreadyProcessed()
            => new(false, LikeOutcome.UserAlreadyProcessed, "You have already liked or passed this user.", null, false);

        public static LikeResult UserBlocked()
            => new(false, LikeOutcome.UserBlocked, "Cannot like a blocked user.", null, false);

        public static LikeResult UserNotFound()
            => new(false, LikeOutcome.UserNotFound, "User not found or inactive.", null, false);
    }

    public class DiscoveryResult : OperationResult<List<UserProfileDTO>>
    {
        public string DiscoveryMode { get; private set; }
        public int RequestedCount { get; private set; }
        public int ActualCount { get; private set; }
        public bool FromCache { get; private set; }

        private DiscoveryResult(bool success, List<UserProfileDTO>? users, string discoveryMode, int requestedCount, bool fromCache, string? errorMessage = null)
            : base(success, users, errorMessage)
        {
            DiscoveryMode = discoveryMode;
            RequestedCount = requestedCount;
            ActualCount = users?.Count ?? 0;
            FromCache = fromCache;
        }

        public static new DiscoveryResult Success(List<UserProfileDTO> users, string discoveryMode, int requestedCount, bool fromCache)
            => new(true, users, discoveryMode, requestedCount, fromCache);

        public static DiscoveryResult NoUsersFound(string discoveryMode, int requestedCount)
            => new(true, [], discoveryMode, requestedCount, false);

        public static DiscoveryResult UserNotFound()
            => new(false, null, "unknown", 0, false, "Current user not found.");

        public static DiscoveryResult LocationRequired()
            => new(false, null, "local", 0, false, "Location is required for local discovery.");
    }
}