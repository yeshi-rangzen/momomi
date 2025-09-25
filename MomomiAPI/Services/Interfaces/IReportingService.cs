using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IReportingService
    {
        /// <summary>
        /// Reports a user for policy violations
        /// </summary>
        Task<OperationResult> ReportUserAsync(ReportUserRequest reportRequest);

        /// <summary>
        /// Blocks a user and removes all interactions
        /// </summary>
        Task<OperationResult> BlockUserAsync(BlockUserRequest blockRequest);

    }
}