using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomomiAPI.Models.Entities
{
    [Table("user_preferences")]
    public class UserPreference
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        [Required]
        public Guid UserId { get; set; }

        [Column("preferred_heritage")]
        public List<HeritageType>? PreferredHeritage { get; set; }

        [Column("preferred_religions")]
        public List<ReligionType>? PreferredReligions { get; set; }

        [Column("cultural_importance_level")]
        public int CulturalImportanceLevel { get; set; } = 5; // 1-10 scale

        [Column("language_preference")]
        public List<string>? LanguagePreference { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
