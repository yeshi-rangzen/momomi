namespace MomomiAPI.Models.DTOs
{
    public class BlockedUserDTO
    {
        public Guid UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PrimaryPhoto { get; set; }
        public string? Reason { get; set; }
        public DateTime BlockedAt { get; set; }
    }

}
