namespace MomomiAPI.Models.DTOs
{
    public class MatchDTO
    {
        public Guid MatchId { get; set; }
        public Guid UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public string? PrimaryPhoto { get; set; }
        public Models.Enums.HeritageType? Heritage { get; set; }
        public DateTime MatchedAt { get; set; }
        public MessageDTO? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }
}
