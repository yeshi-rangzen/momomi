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
    public class SwipeData
    {
        public SwipeOutcome Outcome { get; set; }
        public Guid? SwipedUserId { get; set; }
        public bool IsMatch { get; set; } = false;
        public string? SwipeType { get; set; } // "Like", "SuperLike", "Pass"
        public DateTime SwipeTimestamp { get; set; } = DateTime.UtcNow;

    }
    public class SwipeResult : OperationResult<SwipeData>
    {
        public SwipeOutcome Outcome { get; private set; }
        public Guid? SwipedUserId { get; private set; }

        private SwipeResult(bool success, SwipeData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static SwipeResult MatchCreated(Guid swipedUserId, string swipeType = "Like", Dictionary<string, object>? metadata = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.MatchCreated,
                SwipedUserId = swipedUserId,
                IsMatch = true,
                SwipeType = swipeType,
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(true, swipeData, null, null, metadata);
        }

        public static SwipeResult LikeRecorded(Guid swipedUserId)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.Success,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "Like",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(true, swipeData);
        }

        public static SwipeResult SuperLikeRecorded(Guid swipedUserId)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.Success,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "SuperLike",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(true, swipeData);
        }

        public static SwipeResult PassRecorded(Guid swipedUserId)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.Success,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "Pass",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(true, swipeData);
        }

        public static SwipeResult SwipeUndone(Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.SwipeUndone,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "Undo",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(true, swipeData);
        }
        public static SwipeResult NoRecentPassToUndo()
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.NoRecentPassToUndo,
                SwipedUserId = null,
                IsMatch = false,
                SwipeType = "Undo",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.NO_RECENT_PASS_TO_UNDO,
                "No recent pass swipe found to undo (within last 5 minutes)");
        }

        public static SwipeResult LimitReached(string errorMessage, Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.LimitReached,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "Like",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.LIKE_LIMIT_REACHED, errorMessage);
        }

        public static SwipeResult SuperLikeLimitReached(string errorMessage, Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.LimitReached,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = "SuperLike",
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.SUPER_LIKE_LIMIT_REACHED, errorMessage);
        }

        public static SwipeResult UserAlreadyProcessed(Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.UserAlreadyProcessed,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = null,
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.USER_ALREADY_PROCESSED,
                "You have already swiped on this user.");
        }

        public static SwipeResult UserBlocked(Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.UserBlocked,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = null,
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.USER_BLOCKED,
                "Cannot interact with a blocked user.");
        }
        public static SwipeResult UserNotFound(Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.UserNotFound,
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = null,
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.USER_NOT_FOUND,
                "User not found or inactive.");
        }

        public static SwipeResult Error(string errorMessage, Guid? swipedUserId = null)
        {
            var swipeData = new SwipeData
            {
                Outcome = SwipeOutcome.UserNotFound, // Or consider adding SwipeOutcome.InternalError
                SwipedUserId = swipedUserId,
                IsMatch = false,
                SwipeType = null,
                SwipeTimestamp = DateTime.UtcNow
            };
            return new SwipeResult(false, swipeData, ErrorCodes.INTERNAL_SERVER_ERROR, errorMessage);
        }
    }
}
