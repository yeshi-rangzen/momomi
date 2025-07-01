using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMatchingService
    {
        Task<List<UserProfileDTO>> GetDiscoveryUsersAsync(Guid userId, int count = 10);
        Task<bool> LikeUserAsync(Guid likerUserId, Guid likedUserId);
        Task<bool> PassUserAsync(Guid likerUserId, Guid likedUserId);
        Task<List<MatchDTO>> GetUserMatchesAsync(Guid userId);
        Task<bool> UnmatchAsync(Guid userId, Guid matchedUserId);
    }
}
