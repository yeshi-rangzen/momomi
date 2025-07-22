using FluentValidation;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Validators
{
    public class UpdatePushTokenRequestValidator : AbstractValidator<UpdatePushTokenRequest>
    {
        public UpdatePushTokenRequestValidator()
        {
            RuleFor(x => x.PushToken)
                .NotEmpty().WithMessage("Push token is required")
                .MaximumLength(500).WithMessage("Push token too long");

            //RuleFor(x => x.Platform)
            //    .Must(BeValidPlatform).WithMessage("Valid platform is required (ios, android, web)");
        }

        private bool BeValidPlatform(string? platform)
        {
            if (string.IsNullOrEmpty(platform))
                return false;

            var validPlatforms = new[] { "ios", "android", "web" };
            return validPlatforms.Contains(platform.ToLower());
        }
    }

    public class UpdateNotificationSettingsRequestValidator : AbstractValidator<UpdateNotificationSettingsRequest>
    {
        public UpdateNotificationSettingsRequestValidator()
        {
            RuleFor(x => x.NotificationsEnabled)
                .NotNull().WithMessage("Push notification preference is required");

            //RuleFor(x => x.EnablePushNotifications)
            //    .NotNull().WithMessage("Push notification preference is required");

            //RuleFor(x => x.EnableMatchNotifications)
            //    .NotNull().WithMessage("Match notification preference is required");

            //RuleFor(x => x.EnableMessageNotifications)
            //    .NotNull().WithMessage("Message notification preference is required");

            //RuleFor(x => x.EnableSuperLikeNotifications)
            //    .NotNull().WithMessage("Super like notification preference is required");
        }
    }
}
