using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IBlockingService
    {
        Task<bool> BlockUserAsync(Guid blockerUserId, Guid blockedUserId, string? reason = null);
        Task<bool> UnblockUserAsync(Guid blockerUserId, Guid blockedUserId);
        Task<List<BlockedUserDTO>> GetBlockedUsersAsync(Guid userId);
        Task<bool> IsUserBlockedAsync(Guid userId, Guid targetUserId);
        Task<bool> ReportUserAsync(Guid reporterId, Guid reportedUserId, ReportReason reason, string? description = null);
        Task<List<UserReportDTO>> GetUserReportsAsync(Guid userId);
        Task<bool> IsUserReportedAsync(Guid reporterId, Guid reportedUserId);
    }
}
