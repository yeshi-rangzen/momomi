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

        public class RegisterWithOtpRequest
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(6, MinimumLength = 6)]
            public string Otp { get; set; } = string.Empty;

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
