using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class UpdatePushTokenRequest
    {
        [Required]
        [MaxLength(500)]
        public string PushToken { get; set; } = string.Empty;
    }
}
