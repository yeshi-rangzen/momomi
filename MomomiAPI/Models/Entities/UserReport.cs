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

        [Column("reporter_id")]
        [Required]
        public Guid ReporterId { get; set; }

        [Column("reported_id")]
        [Required]
        public Guid ReportedId { get; set; }

        [Column("reason")]
        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "pending"; // pending, reviewed, resolved

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ReporterId")]
        public virtual User Reporter { get; set; } = null!;

        [ForeignKey("ReportedId")]
        public virtual User Reported { get; set; } = null!;
    }
}
