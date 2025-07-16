using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IReportingService
    {
        Task<OperationResult> ReportUserAsync(Guid reporterId, Guid reportedUserId, ReportReason reason, string? description = null);
        Task<OperationResult<List<UserReportDTO>>> GetUserReportsAsync(Guid userId);
        Task<OperationResult<bool>> IsUserReportedAsync(Guid reporterId, Guid reportedUserId);
    }
}
