using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class UserProfileDTO
    {
        public Guid Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public GenderType? Gender { get; set; }
        public string? Bio { get; set; }
        public HeritageType? Heritage { get; set; }
        public ReligionType? Religion { get; set; }
        public List<string>? LanguagesSpoken { get; set; }
        public string? EducationLevel { get; set; }
        public string? Occupation { get; set; }
        public int? HeightCm { get; set; }
        public double? DistanceKm { get; set; }
        public List<UserPhotoDTO> Photos { get; set; } = new();
        public bool IsVerified { get; set; }
        public DateTime LastActive { get; set; }
    }

    public class UserPhotoDTO
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int PhotoOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
