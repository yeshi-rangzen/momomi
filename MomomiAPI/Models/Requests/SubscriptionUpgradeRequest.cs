using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class SubscriptionUpgradeRequest
    {
        [Range(1, 12)]
        public int DurationMonths { get; set; } = 1;

        public string? PaymentToken { get; set; } // For payment processing
    }
}
