using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Gets user profile with caching optimization
        /// </summary>
        Task<UserProfileResult> GetUserProfileAsync(Guid userId);

        /// <summary>
        /// Updates user profile (non-discovery related fields)
        /// </summary>
        Task<ProfileUpdateResult> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request);

        /// <summary>
        /// Gets discovery filters and preferences
        /// </summary>
        Task<OperationResult<DiscoverySettingsDTO>> GetDiscoveryFiltersAsync(Guid userId);

        /// <summary>
        /// Updates discovery filters and preferences with subscription validation
        /// </summary>
        Task<DiscoveryFiltersUpdateResult> UpdateDiscoveryFiltersAsync(Guid userId, UpdateDiscoveryFiltersRequest request);

        /// <summary>
        /// Deactivates user account (soft delete)
        /// </summary>
        Task<AccountDeactivationResult> DeactivateUserAccountAsync(Guid userId);

        /// <summary>
        /// Permanently deletes user account and all associated data
        /// </summary>
        Task<AccountDeletionResult> DeleteUserAccountAsync(Guid userId);

        Task<bool?> IsActiveUser(Guid userId);
    }
}