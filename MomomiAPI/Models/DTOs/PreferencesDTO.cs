using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class PreferencesDTO
    {

        // MEMBER FILTERS (Free tier - available to all users

        public List<HeritageType>? PreferredHeritage { get; set; }

        public List<ReligionType>? PreferredReligions { get; set; }

        public List<LanguageType>? LanguagePreference { get; set; }

        // SUBSCRIBER FILTERS (Paid tier - available to premium users)
        public int? PreferredHeightMin { get; set; } // in cm

        public int? PreferredHeightMax { get; set; } // in cm

        public List<ChildrenStatusType>? PreferredChildren { get; set; }

        public List<FamilyPlanType>? PreferredFamilyPlans { get; set; }

        public List<ViceFrequencyType>? PreferredDrugs { get; set; }

        public List<ViceFrequencyType>? PreferredSmoking { get; set; }

        public List<ViceFrequencyType>? PreferredDrinking { get; set; }

        public List<ViceFrequencyType>? PreferredMarijuana { get; set; }

        public List<EducationLevelType>? PreferredEducationLevels { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    }
}
