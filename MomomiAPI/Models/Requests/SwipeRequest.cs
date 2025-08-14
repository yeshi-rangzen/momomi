using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class SwipeRequest
    {
        [Required]
        public Guid UserId { get; set; }

        public SwipeType SwipeType { get; set; }

        public string DiscoveryMode { get; set; } = "global"; // Default to Global, can be overridden by frontend
    }
}
