using FluentValidation;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Validators
{
    public class LikeUserRequestValidator : AbstractValidator<LikeUserRequest>
    {
        public LikeUserRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required");

            RuleFor(x => x.LikeType)
                .IsInEnum().WithMessage("Valid like type is required");
        }
    }

    public class ReportUserRequestValidator : AbstractValidator<ReportUserRequest>
    {
        public ReportUserRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("Reported user ID is required");

            RuleFor(x => x.Reason)
                .IsInEnum().WithMessage("Valid report reason is required");

            When(x => !string.IsNullOrEmpty(x.Description), () =>
            {
                RuleFor(x => x.Description)
                    .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");
            });
        }
    }
}
