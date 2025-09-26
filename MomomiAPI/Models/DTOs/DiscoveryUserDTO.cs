using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class DiscoveryUserDTO
    {
        // Identity properties
        public Guid Id { get; set; }

        // Personal information
        public required string FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public GenderType Gender { get; set; }
        public int? HeightCm { get; set; }
        public string? Bio { get; set; }

        // Location information
        public double Latitude { get; set; }
        public double Longitude { get; set; }
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

        // Account status & Verification
        public bool IsVerified { get; set; } = false;

        // Related DTOs
        public List<UserPhotoDTO> Photos { get; set; } = [];
    }

}
