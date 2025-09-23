using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IReportingService
    {
        /// <summary>
        /// Reports a user for policy violations
        /// </summary>
        Task<UserReportResult> ReportUserAsync(ReportUserRequest reportRequest);

        /// <summary>
        /// Blocks a user and removes all interactions
        /// </summary>
        Task<BlockUserResult> BlockUserAsync(BlockUserRequest blockRequest);

    }
}