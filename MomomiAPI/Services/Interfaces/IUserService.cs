using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserService
    {
        Task<OperationResult<User>> GetUserByIdAsync(Guid userId);
        Task<OperationResult<User>> GetUserBySupabaseUidAsync(Guid supabaseUid);
        Task<OperationResult<UserProfileDTO>> GetUserProfileAsync(Guid userId);
        Task<OperationResult> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<OperationResult> DeactivateUserAsync(Guid userId);
        Task<OperationResult<List<UserProfileDTO>>> GetNearbyUsersAsync(Guid userId, int maxDistance);
        Task<OperationResult> UpdateDiscoveryStatusAsync(Guid userId, bool isDiscoverable);
        Task<OperationResult> DeleteUserAsync(Guid userId);

        Task<OperationResult<DiscoverySettingsDTO>> GetDiscoverySettingsAsync(Guid userId);
        Task<OperationResult<DiscoverySettingsDTO>> UpdateDiscoverySettingsAsync(Guid userId, UpdateDiscoverySettingsRequest request);
    }
}
