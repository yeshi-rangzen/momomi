namespace MomomiAPI.Models.DTOs
{
    public class MatchDTO
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public required string Name { get; set; }
        public string? Photo { get; set; }
        public bool IsSuperLiked { get; set; }
        public int UnreadCount { get; set; }
        public MessageDTO? LastMessage { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
