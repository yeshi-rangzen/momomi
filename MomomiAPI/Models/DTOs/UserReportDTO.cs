using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class UserReportDTO
    {
        public Guid Id { get; set; }
        public Guid ReportedUserId { get; set; }
        public string ReportedUserName { get; set; } = string.Empty;
        public string? PrimaryPhoto { get; set; }
        public ReportReason Reason { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
