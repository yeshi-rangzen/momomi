using MomomiAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace MomomiAPI.Models.Requests
{
    public class LikeUserRequest
    {
        [Required]
        public Guid UserId { get; set; }

        public LikeType LikeType { get; set; } = LikeType.Regular;
    }
}
