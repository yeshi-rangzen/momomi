using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("push_notifications")]
    public class PushNotification
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("title")]
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Column("notification_type")]
        public NotificationType NotificationType { get; set; }

        [Column("data")]
        public string? Data { get; set; } // JSON data for additional info

        [Column("is_sent")]
        public bool IsSent { get; set; } = false;

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
