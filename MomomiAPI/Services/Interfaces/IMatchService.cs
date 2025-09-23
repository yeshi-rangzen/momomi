using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMatchService
    {
        Task<MatchResult> GetMatchConversations(Guid currentUserId);
        Task<OperationResult> RemoveMatchConversation(Guid currentUserId, Guid targetUserId, SwipeType removedSwipeType);

        Task<OperationResult<DiscoveryUserDTO>> GetMatchedUser(Guid currentUserId, Guid matchedUserId);

    }
}
