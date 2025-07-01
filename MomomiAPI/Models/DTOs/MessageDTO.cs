namespace MomomiAPI.Models.DTOs
{
    public class MessageDTO
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "text";
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }
    }
}
