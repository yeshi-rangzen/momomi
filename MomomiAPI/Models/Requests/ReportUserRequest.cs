using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    /// <summary>
    /// Request to report a user for policy violations
    /// </summary>
    public class ReportUserRequest
    {
        [Required]
        public Guid ReportedUserId { get; set; }

        [Required]
        public ReportReason Reason { get; set; }

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Request to block a user
    /// </summary>
    public class BlockUserRequest
    {
        [Required]
        public Guid BlockedUserId { get; set; }
    }

    /// <summary>
    /// Request to unblock a user
    /// </summary>
    public class UnblockUserRequest
    {
        [Required]
        public Guid UnblockedUserId { get; set; }
    }

    /// <summary>
    /// Request to get paginated reports
    /// </summary>
    public class GetReportsRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to get paginated blocked users
    /// </summary>
    public class GetBlockedUsersRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 50;
    }
}