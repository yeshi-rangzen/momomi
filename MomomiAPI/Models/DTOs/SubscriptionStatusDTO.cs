using MomomiAPI.Models.Enums;

namespace MomomiAPI.Models.DTOs
{
    public class SubscriptionStatusDTO
    {
        public SubscriptionType SubscriptionType { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? DaysRemaining { get; set; }
    }
}
