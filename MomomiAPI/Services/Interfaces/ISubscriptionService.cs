using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface ISubscriptionService
    {
        Task<OperationResult<SubscriptionStatusDTO>> GetUserSubscriptionAsync(Guid userId);
        Task<OperationResult> UpgradeToPremiumAsync(Guid userId, int durationMonths = 1);
        Task<OperationResult> CancelSubscriptionAsync(Guid userId);
        Task<OperationResult<UsageLimitsDTO>> GetUsageLimitsAsync(Guid userId);
        Task<OperationResult<bool>> CanUserLikeAsync(Guid userId, LikeType likeType = LikeType.Regular);
        OperationResult<bool> CanUserMatchAsync(Guid userId);
        Task<OperationResult> RecordLikeUsageAsync(Guid userId, LikeType likeType);
        Task<OperationResult> RecordAdWatchedAsync(Guid userId);
        Task<OperationResult> ResetDailyLimitsAsync(Guid userId);
        Task<OperationResult> ResetWeeklyLimitsAsync(Guid userId);
    }
}
