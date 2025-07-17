using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.Requests
{
    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Bio { get; set; }
        public List<HeritageType>? Heritage { get; set; }
        public List<ReligionType>? Religion { get; set; }
        public List<LanguageType>? LanguagesSpoken { get; set; }
        public EducationLevelType? EducationLevel { get; set; }
        public string? Occupation { get; set; }
        public int? HeightCm { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? MaxDistanceKm { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool? EnableGlobalDiscovery { get; set; }
        public bool? IsGloballyDiscoverable { get; set; }
        public bool? IsDiscoverable { get; set; }
        public string? Hometown { get; set; }
        public ChildrenStatusType? Children { get; set; }
        public FamilyPlanType? FamilyPlan { get; set; }
        public ViceFrequencyType? Drugs { get; set; }
        public ViceFrequencyType? Smoking { get; set; }
        public ViceFrequencyType? Marijuana { get; set; }
        public ViceFrequencyType? Drinking { get; set; }

        // MEMBER FILTER PREFERENCES (Free users)
        public List<HeritageType>? PreferredHeritage { get; set; }
        public List<ReligionType>? PreferredReligions { get; set; }
        public List<LanguageType>? LanguagePreference { get; set; }


        // SUBCRIBER FILTER PREFERENCES (Premium users only)
        public int? PreferredHeightMin { get; set; }
        public int? PreferredHeightMax { get; set; }
        public List<ChildrenStatusType>? PreferredChildren { get; set; }
        public List<FamilyPlanType>? PreferredFamilyPlans { get; set; }
        public List<ViceFrequencyType>? PreferredDrugs { get; set; }
        public List<ViceFrequencyType>? PreferredSmoking { get; set; }
        public List<ViceFrequencyType>? PreferredMarijuana { get; set; }
        public List<ViceFrequencyType>? PreferredDrinking { get; set; }
        public List<EducationLevelType>? PreferredEducationLevels { get; set; }
    }
}
