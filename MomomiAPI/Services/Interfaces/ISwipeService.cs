using MomomiAPI.Common.Results;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface ISwipeService
    {
        Task<SwipeResult> Swipe(Guid userId, Guid likedUserId, SwipeType swipeType);
        Task<SwipeResult> RewardedSwipe(Guid userId, Guid likedUserId, SwipeType swipeType);
        Task<SwipeResult> UndoSwipe(Guid userId);
    }
}