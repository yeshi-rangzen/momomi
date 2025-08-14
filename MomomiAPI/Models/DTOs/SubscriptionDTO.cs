using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class SubscriptionDTO
    {
        public SubscriptionType SubscriptionType { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
