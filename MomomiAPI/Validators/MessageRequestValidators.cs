using FluentValidation;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Validators
{
    public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.ConversationId)
                .NotEmpty().WithMessage("Conversation ID is required");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Message content is required")
                .Length(1, 1000).WithMessage("Message must be 1-1000 characters");

            RuleFor(x => x.MessageType)
                .MaximumLength(20).WithMessage("Message type cannot exceed 20 characters")
                .Must(BeValidMessageType).WithMessage("Invalid message type");
        }

        private bool BeValidMessageType(string? messageType)
        {
            if (string.IsNullOrEmpty(messageType))
                return true; // Default will be used

            var validTypes = new[] { "text", "image", "emoji", "gif" };
            return validTypes.Contains(messageType.ToLower());
        }
    }
}
