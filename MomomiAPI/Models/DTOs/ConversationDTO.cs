namespace MomomiAPI.Models.DTOs
{
    public class ConversationDTO
    {
        public Guid Id { get; set; }
        public Guid OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public string? OtherUserPhoto { get; set; }
        public MessageDTO? LastMessage { get; set; }
        public int UnreadCount { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
