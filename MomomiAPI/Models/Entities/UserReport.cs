using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_reports")]
    public class UserReport
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("reporter_email")]
        [Required]
        public required string ReporterEmail { get; set; }

        [Column("reported_email")]
        [Required]
        public required string ReportedEmail { get; set; }

        [Column("reported_gender")]
        public GenderType? ReportedGender { get; set; }

        [Column("reason")]
        public ReportReason Reason { get; set; } = ReportReason.Other;

        [Column("description")]
        public string? Description { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "pending"; // pending, reviewed, resolved

        [Column("admin_notes")]
        public string? AdminNotes { get; set; }

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }


        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    }
}
