using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.Requests
{
    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Bio { get; set; }
        public HeritageType? Heritage { get; set; }
        public ReligionType? Religion { get; set; }
        public List<string>? LanguagesSpoken { get; set; }
        public string? EducationLevel { get; set; }
        public string? Occupation { get; set; }
        public int? HeightCm { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? MaxDistanceKm { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool? EnableGlobalDiscovery { get; set; }

        // Preferences
        public List<HeritageType>? PreferredHeritage { get; set; }
        public List<ReligionType>? PreferredReligions { get; set; }
        public int? CulturalImportanceLevel { get; set; }
        public List<string>? LanguagePreference { get; set; }
    }
}
