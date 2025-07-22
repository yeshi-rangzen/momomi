using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class AuthenticationRequests
    {
        public class SendOtpRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public class VerifyOtpRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
            public string Otp { get; set; } = string.Empty;
        }

        public class CompleteRegistrationRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            public string VerificationToken { get; set; } = string.Empty;

            [Required]
            [MaxLength(100)]
            public string FirstName { get; set; } = string.Empty;

            [MaxLength(100)]
            public string? LastName { get; set; }

            [Required]
            public DateTime DateOfBirth { get; set; }

            [Required]
            public GenderType Gender { get; set; }

            [Required]
            public GenderType InterestedIn { get; set; }

            [Required]
            public string Hometown { get; set; } = string.Empty;

            [Required]
            public double Latitude { get; set; }

            [Required]
            public double Longitude { get; set; }
            public int? MaxDistanceKm { get; set; }

            [Required]
            public List<HeritageType> Heritage { get; set; } = [];

            [Required]
            public List<ReligionType> Religion { get; set; } = [];

            [Required]
            public List<LanguageType> LanguagesSpoken { get; set; } = [];
            public string Bio { get; set; } = String.Empty;

            [Phone]
            public string? PhoneNumber { get; set; }
        }

        public class LoginWithOtpRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(6, MinimumLength = 6)]
            public string Otp { get; set; } = string.Empty;
        }

        public class ResendOtpRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public class RefreshTokenRequest
        {
            [Required]
            public string RefreshToken { get; set; } = string.Empty;
        }
    }
}
