using FluentValidation;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Validators
{
    public class SubscriptionUpgradeRequestValidator : AbstractValidator<SubscriptionUpgradeRequest>
    {
        public SubscriptionUpgradeRequestValidator()
        {
            //RuleFor(x => x.SubscriptionTier)
            //    .IsInEnum().WithMessage("Valid subscription tier is required");

            //RuleFor(x => x.PaymentMethod)
            //    .NotEmpty().WithMessage("Payment method is required")
            //    .MaximumLength(50).WithMessage("Payment method name too long");

            //When(x => !string.IsNullOrEmpty(x.PromoCode), () =>
            //{
            //    RuleFor(x => x.PromoCode)
            //        .Length(3, 20).WithMessage("Promo code must be 3-20 characters")
            //        .Matches(@"^[A-Za-z0-9]+$").WithMessage("Promo code can only contain letters and numbers");
            //});
        }
    }
}
