using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface ISubscriptionService
    {
        Task<SubscriptionStatusDTO> GetUserSubscriptionAsync(Guid userId);
        Task<bool> UpgradeToPremiumAsync(Guid userId, int durationMonths = 1);
        Task<bool> CancelSubscriptionAsync(Guid userId);
        Task<UsageLimitsDTO> GetUsageLimitsAsync(Guid userId);
        Task<bool> CanUserLikeAsync(Guid userId, LikeType likeType = LikeType.Regular);
        bool CanUserMatchAsync(Guid userId);
        Task<bool> RecordLikeUsageAsync(Guid userId, LikeType likeType);
        Task<bool> RecordAdWatchedAsync(Guid userId);
        Task<bool> ResetDailyLimitsAsync(Guid userId);
        Task<bool> ResetWeeklyLimitsAsync(Guid userId);
    }
}
