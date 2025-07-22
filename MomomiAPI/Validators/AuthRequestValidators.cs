using FluentValidation;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Validators
{
    public class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
    {
        public SendOtpRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(320).WithMessage("Email too long");
        }
    }

    public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
    {
        public VerifyOtpRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Otp)
                .NotEmpty().WithMessage("OTP is required")
                .Length(6).WithMessage("OTP must be 6 digits")
                .Matches(@"^\d{6}$").WithMessage("OTP must contain only numbers");
        }
    }

    public class CompleteRegistrationRequestValidator : AbstractValidator<CompleteRegistrationRequest>
    {
        public CompleteRegistrationRequestValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .Length(1, 50).WithMessage("First name must be 1-50 characters")
                .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .Length(1, 50).WithMessage("Last name must be 1-50 characters")
                .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes");

            RuleFor(x => x.DateOfBirth)
                .NotEmpty().WithMessage("Date of birth is required")
                .Must(BeValidAge).WithMessage("You must be at least 18 years old")
                .Must(BeReasonableAge).WithMessage("Invalid date of birth");

            RuleFor(x => x.Gender)
                .IsInEnum().WithMessage("Valid gender is required");

            RuleFor(x => x.InterestedIn)
                .IsInEnum().WithMessage("Valid interested in preference is required");

            RuleFor(x => x.Heritage)
                .NotNull().WithMessage("Heritage is required")
                .Must(x => x != null && x.Any()).WithMessage("At least one heritage must be selected")
                .Must(x => x == null || x.Count <= 3).WithMessage("Maximum 3 heritage types allowed");

            RuleFor(x => x.Religion)
                .Must(x => x == null || x.Count <= 2).WithMessage("Maximum 2 religions allowed");

            RuleFor(x => x.LanguagesSpoken)
                .Must(x => x == null || x.Count <= 10).WithMessage("Maximum 10 languages allowed");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Invalid latitude");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Invalid longitude");

            RuleFor(x => x.MaxDistanceKm)
                .GreaterThan(0).WithMessage("Max distance must be greater than 0")
                .LessThanOrEqualTo(500).WithMessage("Max distance cannot exceed 500km");
        }

        private bool BeValidAge(DateTime dateOfBirth)
        {
            var age = DateTime.Today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
            return age >= 18;
        }

        private bool BeReasonableAge(DateTime dateOfBirth)
        {
            var age = DateTime.Today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;
            return age >= 18 && age <= 100;
        }
    }

    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required");
        }
    }
}
