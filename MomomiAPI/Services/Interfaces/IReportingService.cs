using MomomiAPI.Common.Results;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IReportingService
    {
        /// <summary>
        /// Reports a user for policy violations
        /// </summary>
        Task<UserReportResult> ReportUserAsync(Guid reporterId, Guid reportedUserId, ReportReason reason, string? description = null);

        /// <summary>
        /// Blocks a user and removes all interactions
        /// </summary>
        Task<BlockUserResult> BlockUserAsync(Guid blockerId, Guid blockedUserId);

        /// <summary>
        /// Gets user's submitted reports with pagination
        /// </summary>
        Task<UserReportsListResult> GetUserReportsAsync(Guid userId, int page = 1, int pageSize = 20);

        /// <summary>
        /// Gets user's blocked users list with pagination
        /// </summary>
        Task<BlockedUsersListResult> GetBlockedUsersAsync(Guid userId, int page = 1, int pageSize = 50);

        /// <summary>
        /// Unblocks a previously blocked user
        /// </summary>
        Task<UnblockUserResult> UnblockUserAsync(Guid blockerId, Guid blockedUserId);

        /// <summary>
        /// Checks if a user is blocked by another user (cached)
        /// </summary>
        Task<bool> IsUserBlockedAsync(Guid userId, Guid potentialBlockedUserId);
    }
}