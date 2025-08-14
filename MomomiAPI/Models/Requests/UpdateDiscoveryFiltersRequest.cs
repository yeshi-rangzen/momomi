using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    /// <summary>
    /// Comprehensive discovery filters request including basic discovery settings and preferences
    /// </summary>
    public class UpdateDiscoveryFiltersRequest
    {
        // === BASIC DISCOVERY SETTINGS (Available to all users) ===

        // Core Discovery Controls
        public bool? IsDiscoverable { get; set; }
        public bool? IsGloballyDiscoverable { get; set; }
        public bool? EnableGlobalDiscovery { get; set; }

        // Gender & Age Preferences
        public GenderType? InterestedIn { get; set; }

        [Range(18, 100)]
        public int? MinAge { get; set; }

        [Range(18, 100)]
        public int? MaxAge { get; set; }

        // Location & Distance
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(200)]
        public string? Neighbourhood { get; set; }

        [Range(1, 200)]
        public int? MaxDistanceKm { get; set; }

        // === FREE USER FILTERS (Available to all users) ===

        // Cultural Preferences
        public List<HeritageType>? PreferredHeritage { get; set; }
        public List<ReligionType>? PreferredReligions { get; set; }
        public List<LanguageType>? PreferredLanguagesSpoken { get; set; }

        // === PREMIUM SUBSCRIBER FILTERS (Premium users only) ===

        // Physical Preferences
        [Range(120, 250)]
        public int? PreferredHeightMin { get; set; }

        [Range(120, 250)]
        public int? PreferredHeightMax { get; set; }

        // Education & Family Preferences
        public List<EducationLevelType>? PreferredEducationLevels { get; set; }
        public List<ChildrenStatusType>? PreferredChildren { get; set; }
        public List<FamilyPlanType>? PreferredFamilyPlans { get; set; }

        // Lifestyle Preferences
        public List<ViceFrequencyType>? PreferredDrugs { get; set; }
        public List<ViceFrequencyType>? PreferredSmoking { get; set; }
        public List<ViceFrequencyType>? PreferredDrinking { get; set; }
        public List<ViceFrequencyType>? PreferredMarijuana { get; set; }

        /// <summary>
        /// Validates age range consistency
        /// </summary>
        public bool IsValidAgeRange()
        {
            if (MinAge.HasValue && MaxAge.HasValue)
            {
                return MinAge.Value < MaxAge.Value;
            }
            return true;
        }

        /// <summary>
        /// Validates height range consistency  
        /// </summary>
        public bool IsValidHeightRange()
        {
            if (PreferredHeightMin.HasValue && PreferredHeightMax.HasValue)
            {
                return PreferredHeightMin.Value < PreferredHeightMax.Value;
            }
            return true;
        }

        /// <summary>
        /// Validates location coordinates
        /// </summary>
        public bool IsValidLocation()
        {
            if (Latitude.HasValue || Longitude.HasValue)
            {
                return Latitude.HasValue && Longitude.HasValue &&
                       Latitude.Value >= -90 && Latitude.Value <= 90 &&
                       Longitude.Value >= -180 && Longitude.Value <= 180;
            }
            return true;
        }

        /// <summary>
        /// Gets all premium filter properties that are being updated
        /// </summary>
        public List<string> GetPremiumFiltersBeingUpdated()
        {
            var premiumFilters = new List<string>();

            if (PreferredHeightMin.HasValue) premiumFilters.Add(nameof(PreferredHeightMin));
            if (PreferredHeightMax.HasValue) premiumFilters.Add(nameof(PreferredHeightMax));
            if (PreferredEducationLevels != null) premiumFilters.Add(nameof(PreferredEducationLevels));
            if (PreferredChildren != null) premiumFilters.Add(nameof(PreferredChildren));
            if (PreferredFamilyPlans != null) premiumFilters.Add(nameof(PreferredFamilyPlans));
            if (PreferredDrugs != null) premiumFilters.Add(nameof(PreferredDrugs));
            if (PreferredSmoking != null) premiumFilters.Add(nameof(PreferredSmoking));
            if (PreferredDrinking != null) premiumFilters.Add(nameof(PreferredDrinking));
            if (PreferredMarijuana != null) premiumFilters.Add(nameof(PreferredMarijuana));

            return premiumFilters;
        }
    }
}
