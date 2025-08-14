using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("conversations")]
    public class Conversation
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user1_id")]
        [Required]
        public Guid User1Id { get; set; }

        [Column("user2_id")]
        [Required]
        public Guid User2Id { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("swipe_type")]
        public SwipeType SwipeType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("User1Id")]
        public virtual User User1 { get; set; } = null!;

        [ForeignKey("User2Id")]
        public virtual User User2 { get; set; } = null!;

        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
