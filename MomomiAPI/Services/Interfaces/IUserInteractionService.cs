using MomomiAPI.Common.Results;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserInteractionService
    {
        Task<LikeResult> ExpressInterest(Guid likerId, Guid likedId, LikeType interestType = LikeType.Regular);
        Task<OperationResult> DismissUser(Guid dismisserId, Guid dismissedId);
        Task<OperationResult> UndoLastSwipe(Guid userId);
    }
}