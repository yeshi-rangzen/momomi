using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_usage_limits")]
    public class UserUsageLimit
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("likes_used_today")]
        public int LikesUsedToday { get; set; } = 0;

        [Column("super_likes_used_today")]
        public int SuperLikesUsedToday { get; set; } = 0;

        [Column("ads_watched_today")]
        public int AdsWatchedToday { get; set; } = 0;

        [Column("last_reset_date")]
        public DateTime LastResetDate { get; set; } = DateTime.UtcNow.Date;


        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
