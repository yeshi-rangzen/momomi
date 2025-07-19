using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class UserLikeDTO
    {
        public Guid UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public string? PrimaryPhoto { get; set; }
        public List<HeritageType>? Heritage { get; set; }
        public LikeType LikeType { get; set; }
        public DateTime LikedAt { get; set; }
        public double? DistanceKm { get; set; }
    }
}
