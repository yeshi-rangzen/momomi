using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Common.Results
{
    public enum InteractionOutcome
    {
        Success,
        LimitReached,
        UserAlreadyProcessed,
        UserBlocked,
        UserNotFound,
        MatchCreated
    }
    public class InteractionResult : OperationResult
    {
        public InteractionOutcome Outcome { get; private set; }
        public UsageLimitsDTO? UpdatedLimits { get; private set; }
        public bool IsMatch { get; private set; }
        public Guid? TargetUserId { get; private set; }
        public LikeType? InteractionType { get; private set; }

        private InteractionResult(bool success, InteractionOutcome outcome, string? errorMessage,
            UsageLimitsDTO? updatedLimits, bool isMatch, Guid? targetUserId, LikeType? interactionType)
            : base(success, errorMessage)
        {
            Outcome = outcome;
            UpdatedLimits = updatedLimits;
            IsMatch = isMatch;
            TargetUserId = targetUserId;
            InteractionType = interactionType;
        }

        public static InteractionResult MatchCreated(Guid targetUserId, LikeType interactionType, UsageLimitsDTO updatedLimits)
            => new(true, InteractionOutcome.MatchCreated, null, updatedLimits, true, targetUserId, interactionType);

        public static InteractionResult LikeRecorded(Guid targetUserId, LikeType interactionType, UsageLimitsDTO updatedLimits)
            => new(true, InteractionOutcome.Success, null, updatedLimits, false, targetUserId, interactionType);

        public static InteractionResult PassRecorded(Guid targetUserId)
            => new(true, InteractionOutcome.Success, null, null, false, targetUserId, null);

        public static InteractionResult UndoSuccessful(Guid targetUserId)
            => new(true, InteractionOutcome.Success, null, null, false, targetUserId, null);

        public static InteractionResult LimitReached(string message, UsageLimitsDTO? currentLimits, LikeType interactionType)
            => new(false, InteractionOutcome.LimitReached, message, currentLimits, false, null, interactionType);

        public static InteractionResult UserAlreadyProcessed(Guid targetUserId)
            => new(false, InteractionOutcome.UserAlreadyProcessed, "You have already interacted with this user.", null, false, targetUserId, null);

        public static InteractionResult UserBlocked(Guid targetUserId)
            => new(false, InteractionOutcome.UserBlocked, "Cannot interact with a blocked user.", null, false, targetUserId, null);

        public static InteractionResult UserNotFound()
            => new(false, InteractionOutcome.UserNotFound, "User not found or inactive.", null, false, null, null);

        public static InteractionResult Failed(string errorMessage)
            => new(false, InteractionOutcome.UserNotFound, errorMessage, null, false, null, null);
    }
}
