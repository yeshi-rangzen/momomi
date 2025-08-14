using MomomiAPI.Common.Results;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMatchService
    {
        Task<MatchResult> GetMatchConversations(Guid currentUserId);
        Task<OperationResult> RemoveMatchConversation(Guid currentUserId, Guid targetUserId, SwipeType removedSwipeType);

    }
}
