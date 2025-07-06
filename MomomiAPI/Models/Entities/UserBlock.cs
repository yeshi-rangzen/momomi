using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_blocks")]
    public class UserBlock
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("blocker_user_id")]
        [Required]
        public Guid BlockerUserId { get; set; }

        [Column("blocked_user_id")]
        [Required]
        public Guid BlockedUserId { get; set; }

        [Column("reason")]
        [MaxLength(500)]
        public string? Reason { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("BlockerUserId")]
        public virtual User BlockerUser { get; set; } = null!;

        [ForeignKey("BlockedUserId")]
        public virtual User BlockedUser { get; set; } = null!;
    }
}
