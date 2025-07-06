using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class BlockUserRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
