using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    /// Request for updating basic profile information (non-discovery related)
    public class UpdateProfileRequest
    {
        // Personal Information
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }

        [MaxLength(200)]
        public string? Hometown { get; set; }

        [MaxLength(100)]
        public string? Occupation { get; set; }

        // Physical Attributes
        [Range(120, 250)]
        public int? HeightCm { get; set; }

        // Cultural & Background
        public List<HeritageType>? Heritage { get; set; }
        public List<ReligionType>? Religion { get; set; }
        public List<LanguageType>? LanguagesSpoken { get; set; }
        public EducationLevelType? EducationLevel { get; set; }

        // Family & Lifestyle
        public ChildrenStatusType? Children { get; set; }
        public FamilyPlanType? FamilyPlan { get; set; }
        public ViceFrequencyType? Drugs { get; set; }
        public ViceFrequencyType? Smoking { get; set; }
        public ViceFrequencyType? Marijuana { get; set; }
        public ViceFrequencyType? Drinking { get; set; }

        // Notifications
        public bool? NotificationsEnabled { get; set; }
        public string? PushToken { get; set; }
    }
}
