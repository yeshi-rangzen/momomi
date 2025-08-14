using MomomiAPI.Common.Results;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Helpers
{
    public class SwipeUserData
    {
        public User? CurrentUser { get; init; }
        public User? TargetUser { get; init; }
        public bool IsAlreadyLikedByTarget { get; init; }
        public bool IsSuperLikedByTarget { get; init; }
        public bool IsValid { get; init; }
        public SwipeResult? Error { get; init; }

        public static SwipeUserData Valid(User currentUser, User targetUser, bool isAlreadyLikedByTarget, bool isSuperLikedByTarget)
        {
            return new SwipeUserData
            {
                CurrentUser = currentUser,
                TargetUser = targetUser,
                IsAlreadyLikedByTarget = isAlreadyLikedByTarget,
                IsSuperLikedByTarget = isSuperLikedByTarget,
                IsValid = true
            };
        }

        public static SwipeUserData Invalid(SwipeResult error)
        {
            return new SwipeUserData
            {
                IsValid = false,
                Error = error
            };
        }
    }

    public class PassUserValidation
    {
        public bool IsValid { get; init; }
        public SwipeResult? Error { get; init; }

        public static PassUserValidation Valid()
        {
            return new PassUserValidation { IsValid = true };
        }

        public static PassUserValidation Invalid(SwipeResult error)
        {
            return new PassUserValidation
            {
                IsValid = false,
                Error = error
            };
        }
    }
}
