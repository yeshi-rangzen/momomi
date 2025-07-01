using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("messages")]
    public class Message
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("conversation_id")]
        [Required]
        public Guid ConversationId { get; set; }

        [Column("sender_id")]
        [Required]
        public Guid SenderId { get; set; }

        [Column("content")]
        [Required]
        public string Content { get; set; } = string.Empty;

        [Column("message_type")]
        [MaxLength(20)]
        public string MessageType { get; set; } = "text"; // text, image, emoji

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("sent_at")]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ConversationId")]
        public virtual Conversation Conversation { get; set; } = null!;

        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; } = null!;
    }
}
