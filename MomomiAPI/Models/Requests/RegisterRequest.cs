using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

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
}
