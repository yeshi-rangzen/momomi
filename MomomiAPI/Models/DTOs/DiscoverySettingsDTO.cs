using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class DiscoverySettingsDTO
    {
        // === BASIC DISCOVERY CONTROLS ===
        public bool EnableGlobalDiscovery { get; set; }
        public bool IsDiscoverable { get; set; }
        public bool IsGloballyDiscoverable { get; set; }

        // === CORE MATCHING CRITERIA ===
        public GenderType InterestedIn { get; set; }
        public int MinAge { get; set; }
        public int MaxAge { get; set; }
        public int MaxDistanceKm { get; set; }

        // === LOCATION ===
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Neighbourhood { get; set; }
        public bool HasLocation { get; set; }

        // === FREE USER FILTERS ===
        public List<HeritageType>? PreferredHeritage { get; set; }
        public List<ReligionType>? PreferredReligions { get; set; }
        public List<LanguageType>? PreferredLanguagesSpoken { get; set; }

        // === PREMIUM SUBSCRIBER FILTERS ===
        public int? PreferredHeightMin { get; set; }
        public int? PreferredHeightMax { get; set; }
        public List<EducationLevelType>? PreferredEducationLevels { get; set; }
        public List<ChildrenStatusType>? PreferredChildren { get; set; }
        public List<FamilyPlanType>? PreferredFamilyPlans { get; set; }
        public List<ViceFrequencyType>? PreferredDrugs { get; set; }
        public List<ViceFrequencyType>? PreferredSmoking { get; set; }
        public List<ViceFrequencyType>? PreferredDrinking { get; set; }
        public List<ViceFrequencyType>? PreferredMarijuana { get; set; }

        // === METADATA ===
        public bool IsPremiumUser { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
