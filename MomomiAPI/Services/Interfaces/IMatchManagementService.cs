using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMatchManagementService
    {
        Task<OperationResult<List<MatchDTO>>> GetUserMatches(Guid userId);
        Task<OperationResult> RemoveMatch(Guid userId, Guid matchedUserId);
        Task<OperationResult<bool>> CheckForNewMatch(Guid user1Id, Guid user2Id);
    }
}