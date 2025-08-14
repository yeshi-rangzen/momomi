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

        // MEMBER FILTERS (Free tier - available to all users

        [Column("preferred_heritage")]
        public List<HeritageType>? PreferredHeritage { get; set; }

        [Column("preferred_religions")]
        public List<ReligionType>? PreferredReligions { get; set; }

        [Column("language_preference")]
        public List<LanguageType>? LanguagePreference { get; set; }

        // SUBSCRIBER FILTERS (Paid tier - available to premium users)
        [Column("preferred_height_min")]
        public int? PreferredHeightMin { get; set; } // in cm

        [Column("preferred_height_max")]
        public int? PreferredHeightMax { get; set; } // in cm

        [Column("preferred_education_levels")]
        public List<EducationLevelType>? PreferredEducationLevels { get; set; }

        [Column("preferred_children")]
        public List<ChildrenStatusType>? PreferredChildren { get; set; }

        [Column("preferred_family_plans")]
        public List<FamilyPlanType>? PreferredFamilyPlans { get; set; }

        [Column("preferred_drugs")]
        public List<ViceFrequencyType>? PreferredDrugs { get; set; }

        [Column("preferred_smoking")]
        public List<ViceFrequencyType>? PreferredSmoking { get; set; }

        [Column("preferred_drinking")]
        public List<ViceFrequencyType>? PreferredDrinking { get; set; }

        [Column("preferred_marijuana")]
        public List<ViceFrequencyType>? PreferredMarijuana { get; set; }



        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
