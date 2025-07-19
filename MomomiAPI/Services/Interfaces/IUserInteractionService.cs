using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserInteractionService
    {
        Task<InteractionResult> ExpressInterest(Guid likerId, Guid likedId, LikeType interestType = LikeType.Regular);
        Task<InteractionResult> DismissUser(Guid dismisserId, Guid dismissedId);
        Task<InteractionResult> UndoLastSwipe(Guid userId);
        Task<OperationResult<List<UserLikeDTO>>> GetUsersWhoLikedMe(Guid userId, int page = 1, int pageSize = 20);
    }
}