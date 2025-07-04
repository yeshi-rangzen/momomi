using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_likes")]
    public class UserLike
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("liker_user_id")]
        [Required]
        public Guid LikerUserId { get; set; }

        [Column("liked_user_id")]
        [Required]
        public Guid LikedUserId { get; set; }

        [Column("is_like")]
        public bool IsLike { get; set; } = true; // true for like, false for pass

        [Column("is_match")]
        public bool IsMatch { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("LikerUserId")]
        public virtual User LikerUser { get; set; } = null!;

        [ForeignKey("LikedUserId")]
        public virtual User LikedUser { get; set; } = null!;
    }
}
