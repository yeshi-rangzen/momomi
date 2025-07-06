using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(Guid userId);
        Task<User?> GetUserBySupabaseUidAsync(Guid supabaseUid);
        Task<User?> ValidateAndGetUserAsync(string token);
        Task<UserProfileDTO?> GetUserProfileAsync(Guid userId);
        Task<bool> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<bool> DeactivateUserAsync(Guid userId);
        Task<IEnumerable<UserProfileDTO>> GetNearbyUsersAsync(Guid userId, int maxDistance);
        Task<bool> UpdateDiscoveryStatusAsync(Guid userId, bool isDiscoverable);
        Task<bool> DeleteUserAsync(Guid userId);
    }
}
