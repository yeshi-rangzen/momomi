using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    public enum SwipeOutcome
    {
        Success,
        LimitReached,
        UserAlreadyProcessed,
        UserBlocked,
        UserNotFound,
        MatchCreated,
        SwipeUndone,
        NoRecentPassToUndo
    }
    // TODO: 
    public class SwipeData
    {
        SwipeOutcome Outcome { get; set; }
    }
    public class SwipeResult : OperationResult
    {
        public SwipeOutcome Outcome { get; private set; }

        private SwipeResult(SwipeOutcome outcome, bool success, string? errorCode = null, string? errorMessage = null, Dictionary<string, object>? metadata = null)
        {
            Outcome = outcome;
            Success = success;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Metadata = metadata;
        }

        public static SwipeResult MatchCreated()
            => new(SwipeOutcome.MatchCreated, true);

        public static SwipeResult LikeRecorded()
            => new(SwipeOutcome.Success, true);

        public static SwipeResult SuperLikeRecorded()
            => new(SwipeOutcome.Success, true);

        public static SwipeResult PassRecorded()
            => new(SwipeOutcome.Success, true);

        public static SwipeResult SwipeUndone()
            => new(SwipeOutcome.SwipeUndone, true);
        public static SwipeResult NoRecentPassToUndo()
            => new(SwipeOutcome.NoRecentPassToUndo, false, ErrorCodes.NO_RECENT_PASS_TO_UNDO, "No recent pass swipe found to undo (within last 5 minutes)");

        public static SwipeResult LimitReached(string errorMessage)
            => new(SwipeOutcome.LimitReached, false, ErrorCodes.LIKE_LIMIT_REACHED, errorMessage);

        public static SwipeResult SuperLikeLimitReached(string errorMessage)
            => new(SwipeOutcome.LimitReached, false, ErrorCodes.SUPER_LIKE_LIMIT_REACHED, errorMessage);

        public static SwipeResult UserAlreadyProcessed()
            => new(SwipeOutcome.UserAlreadyProcessed, false, ErrorCodes.USER_ALREADY_PROCESSED, "You have already swiped on this user.");

        public static SwipeResult UserBlocked()
            => new(SwipeOutcome.UserBlocked, false, ErrorCodes.USER_BLOCKED, "Cannot interact with a blocked user.");

        public static SwipeResult UserNotFound()
            => new(SwipeOutcome.UserNotFound, false, ErrorCodes.USER_NOT_FOUND, "User not found or inactive.");

        public static SwipeResult Error(string errorMessage)
            => new(SwipeOutcome.UserNotFound, false, ErrorCodes.INTERNAL_SERVER_ERROR, errorMessage);
    }
}
