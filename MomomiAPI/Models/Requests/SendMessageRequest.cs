using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class SendMessageRequest
    {
        [Required]
        public Guid ConversationId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(20)]
        public string MessageType { get; set; } = "text";
    }
}
