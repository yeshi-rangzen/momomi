using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class ReportUserRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public ReportReason Reason { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
