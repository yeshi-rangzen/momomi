using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class UserDTO
    {
        // Identity properties
        public Guid Id { get; set; }
        public required string Email { get; set; }
        public string? PhoneNumber { get; set; }

        // Personal information
        public required string FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public GenderType Gender { get; set; }
        public int? HeightCm { get; set; }
        public string? Bio { get; set; }

        // Location information
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public required string Hometown { get; set; }
        public string? Neighbourhood { get; set; }

        // Cultural & Background Information
        public string? Occupation { get; set; }
        public EducationLevelType? EducationLevel { get; set; }
        public List<HeritageType> Heritage { get; set; } = [];
        public List<ReligionType> Religion { get; set; } = [];
        public List<LanguageType> LanguagesSpoken { get; set; } = [];

        // Family Plans
        public FamilyPlanType? FamilyPlan { get; set; }
        public ChildrenStatusType? Children { get; set; }

        // Vices
        public ViceFrequencyType? Drugs { get; set; }
        public ViceFrequencyType? Smoking { get; set; }
        public ViceFrequencyType? Marijuana { get; set; }
        public ViceFrequencyType? Drinking { get; set; }

        // Matching & Discovery Preferences
        public GenderType InterestedIn { get; set; }
        public int MinAge { get; set; }
        public int MaxAge { get; set; }
        public int MaxDistanceKm { get; set; }
        public bool IsDiscoverable { get; set; } = true;
        public bool IsGloballyDiscoverable { get; set; } = false;
        public bool EnableGlobalDiscovery { get; set; } = false;

        // Notifications & Device
        public string? PushToken { get; set; }
        public bool NotificaitonsEnabled { get; set; } = true;

        // Account status & Verification
        public bool IsVerified { get; set; } = false;
        public bool IsOnboarding { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Timestampts
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        // Related DTOs
        public List<UserPhotoDTO> Photos { get; set; } = [];
        public required PreferencesDTO Preferences { get; set; }
        public SubscriptionDTO? Subscription { get; set; }
        public UsageLimitsDTO? UsageLimit { get; set; }
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
