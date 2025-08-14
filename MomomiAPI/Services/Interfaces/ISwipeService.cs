using MomomiAPI.Common.Results;

namespace MomomiAPI.Services.Interfaces
{
    public interface ISwipeService
    {
        Task<SwipeResult> LikeUser(Guid userId, Guid likedUserId);
        Task<SwipeResult> SuperLikeUser(Guid userId, Guid likedUserId);
        Task<SwipeResult> PassUser(Guid userId, Guid dismissedUserId);
        Task<SwipeResult> UndoLastSwipe(Guid userId);
    }
}