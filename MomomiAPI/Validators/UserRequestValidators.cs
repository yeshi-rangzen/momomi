using FluentValidation;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Validators
{
    public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
    {
        public UpdateProfileRequestValidator()
        {
            When(x => !string.IsNullOrEmpty(x.FirstName), () =>
            {
                RuleFor(x => x.FirstName)
                    .Length(1, 50).WithMessage("First name must be 1-50 characters")
                    .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes");
            });

            When(x => !string.IsNullOrEmpty(x.LastName), () =>
            {
                RuleFor(x => x.LastName)
                    .Length(1, 50).WithMessage("Last name must be 1-50 characters")
                    .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes");
            });

            When(x => !string.IsNullOrEmpty(x.Bio), () =>
            {
                RuleFor(x => x.Bio)
                    .MaximumLength(500).WithMessage("Bio cannot exceed 500 characters");
            });

            When(x => !string.IsNullOrEmpty(x.Hometown), () =>
            {
                RuleFor(x => x.Hometown)
                    .MaximumLength(100).WithMessage("Hometown cannot exceed 100 characters");
            });

            When(x => !string.IsNullOrEmpty(x.Occupation), () =>
            {
                RuleFor(x => x.Occupation)
                    .MaximumLength(100).WithMessage("Occupation cannot exceed 100 characters");
            });

            When(x => x.Heritage != null, () =>
            {
                RuleFor(x => x.Heritage)
                    .Must(x => x!.Count <= 3).WithMessage("Maximum 3 heritage types allowed");
            });

            When(x => x.Religion != null, () =>
            {
                RuleFor(x => x.Religion)
                    .Must(x => x!.Count <= 2).WithMessage("Maximum 2 religions allowed");
            });

            When(x => x.LanguagesSpoken != null, () =>
            {
                RuleFor(x => x.LanguagesSpoken)
                    .Must(x => x!.Count <= 10).WithMessage("Maximum 10 languages allowed");
            });

            When(x => x.HeightCm.HasValue, () =>
            {
                RuleFor(x => x.HeightCm)
                    .InclusiveBetween(120, 250).WithMessage("Height must be between 120-250 cm");
            });

            When(x => x.Latitude.HasValue, () =>
            {
                RuleFor(x => x.Latitude)
                    .InclusiveBetween(-90, 90).WithMessage("Invalid latitude");
            });

            When(x => x.Longitude.HasValue, () =>
            {
                RuleFor(x => x.Longitude)
                    .InclusiveBetween(-180, 180).WithMessage("Invalid longitude");
            });

            When(x => x.MaxDistanceKm.HasValue, () =>
            {
                RuleFor(x => x.MaxDistanceKm)
                    .GreaterThan(0).WithMessage("Max distance must be greater than 0")
                    .LessThanOrEqualTo(500).WithMessage("Max distance cannot exceed 500km");
            });

            When(x => x.MinAge.HasValue, () =>
            {
                RuleFor(x => x.MinAge)
                    .InclusiveBetween(18, 100).WithMessage("Minimum age must be between 18-100");
            });

            When(x => x.MaxAge.HasValue, () =>
            {
                RuleFor(x => x.MaxAge)
                    .InclusiveBetween(18, 100).WithMessage("Maximum age must be between 18-100");
            });

            // Cross-field validation for age range
            When(x => x.MinAge.HasValue && x.MaxAge.HasValue, () =>
            {
                RuleFor(x => x.MinAge)
                    .LessThanOrEqualTo(x => x.MaxAge).WithMessage("Minimum age cannot be greater than maximum age");
            });

            // Premium feature validations
            When(x => x.PreferredHeightMin.HasValue, () =>
            {
                RuleFor(x => x.PreferredHeightMin)
                    .InclusiveBetween(120, 250).WithMessage("Preferred minimum height must be between 120-250 cm");
            });

            When(x => x.PreferredHeightMax.HasValue, () =>
            {
                RuleFor(x => x.PreferredHeightMax)
                    .InclusiveBetween(120, 250).WithMessage("Preferred maximum height must be between 120-250 cm");
            });

            When(x => x.PreferredHeightMin.HasValue && x.PreferredHeightMax.HasValue, () =>
            {
                RuleFor(x => x.PreferredHeightMin)
                    .LessThanOrEqualTo(x => x.PreferredHeightMax).WithMessage("Minimum height cannot be greater than maximum height");
            });
        }
    }
}
