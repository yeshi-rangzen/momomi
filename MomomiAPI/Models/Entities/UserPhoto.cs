using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_photos")]
    public class UserPhoto
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("cloudinary_public_id")]
        [Required]
        [MaxLength(255)]
        public string CloudinaryPublicId { get; set; } = string.Empty;

        [Column("url")]
        [Required]
        public string Url { get; set; } = string.Empty;

        [Column("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [Column("photo_order")]
        public int PhotoOrder { get; set; } = 0;

        [Column("is_primary")]
        public bool IsPrimary { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
