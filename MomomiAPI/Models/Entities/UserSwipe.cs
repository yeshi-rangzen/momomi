using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_swipes")]
    public class UserSwipe
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("swiper_user_id")]
        [Required]
        public Guid SwiperUserId { get; set; }

        [Column("swiped_user_id")]
        [Required]
        public Guid SwipedUserId { get; set; }

        [Column("swipe_type")]
        public SwipeType SwipeType { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("SwiperUserId")]
        public virtual User SwiperUser { get; set; } = null!;

        [ForeignKey("SwipedUserId")]
        public virtual User SwipedUser { get; set; } = null!;
    }
}
