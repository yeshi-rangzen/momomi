using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    /// User profile data
    public class UserProfileData
    {
        public UserDTO User { get; set; } = null!;
        public DateTime LastUpdated { get; set; }
    }

    public class UserProfileResult : OperationResult<UserProfileData>
    {
        private UserProfileResult(bool success, UserProfileData? data, string? errorCode = null,
           string? errorMessage = null, Dictionary<string, object>? metadata = null)
           : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static UserProfileResult Successful(UserDTO user,
            Dictionary<string, object>? metadata = null)
        {
            var data = new UserProfileData
            {
                User = user,
                LastUpdated = DateTime.UtcNow
            };

            return new UserProfileResult(true, data, null, null, metadata);
        }

        public static UserProfileResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User profile not found");

        public static UserProfileResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Profile update data
    public class ProfileUpdateData
    {
        public List<string> UpdatedFields { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class ProfileUpdateResult : OperationResult<ProfileUpdateData>
    {
        private ProfileUpdateResult(bool success, ProfileUpdateData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static ProfileUpdateResult Successful(List<string> updatedFields, Dictionary<string, object>? metadata = null)
        {
            var data = new ProfileUpdateData
            {
                UpdatedFields = updatedFields,
                UpdatedAt = DateTime.UtcNow
            };

            return new ProfileUpdateResult(true, data, null, null, metadata);
        }

        public static ProfileUpdateResult ValidationError(string message)
            => new(false, null, ErrorCodes.VALIDATION_ERROR, message);

        public static ProfileUpdateResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static ProfileUpdateResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Discovery filters update data
    public class DiscoveryFiltersUpdateData
    {
        public List<string> UpdatedFilters { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class DiscoveryFiltersUpdateResult : OperationResult<DiscoveryFiltersUpdateData>
    {
        private DiscoveryFiltersUpdateResult(bool success, DiscoveryFiltersUpdateData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static DiscoveryFiltersUpdateResult Successful(
            List<string> updatedFilters,
            Dictionary<string, object>? metadata = null)
        {
            var data = new DiscoveryFiltersUpdateData
            {
                UpdatedFilters = updatedFilters,
                UpdatedAt = DateTime.UtcNow
            };

            return new DiscoveryFiltersUpdateResult(true, data, null, null, metadata);
        }

        public static DiscoveryFiltersUpdateResult ValidationError(string message)
            => new(false, null, ErrorCodes.VALIDATION_ERROR, message);

        public static DiscoveryFiltersUpdateResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static DiscoveryFiltersUpdateResult SubscriptionRequired(string filter)
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                $"Premium subscription required to use {filter} filter");

        public static DiscoveryFiltersUpdateResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Account deletion data
    public class AccountDeletionData
    {
        public Guid DeletedUserId { get; set; }
        public int PhotosDeleted { get; set; }
        public int ConversationsDeleted { get; set; }
        public int SwipesDeleted { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    public class AccountDeletionResult : OperationResult<AccountDeletionData>
    {
        private AccountDeletionResult(bool success, AccountDeletionData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static AccountDeletionResult Successful(Guid userId, int photosDeleted,
            int conversationsDeleted, int swipesDeleted, Dictionary<string, object>? metadata = null)
        {
            var data = new AccountDeletionData
            {
                DeletedUserId = userId,
                PhotosDeleted = photosDeleted,
                ConversationsDeleted = conversationsDeleted,
                SwipesDeleted = swipesDeleted,
                DeletedAt = DateTime.UtcNow
            };

            return new AccountDeletionResult(true, data, null, null, metadata);
        }

        public static AccountDeletionResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static AccountDeletionResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Account deactivation data
    public class AccountDeactivationData
    {
        public Guid UserId { get; set; }
        public bool WasActive { get; set; }
        public DateTime DeactivatedAt { get; set; }
    }

    public class AccountDeactivationResult : OperationResult<AccountDeactivationData>
    {
        private AccountDeactivationResult(bool success, AccountDeactivationData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static AccountDeactivationResult Successful(Guid userId, bool wasActive,
            Dictionary<string, object>? metadata = null)
        {
            var data = new AccountDeactivationData
            {
                UserId = userId,
                WasActive = wasActive,
                DeactivatedAt = DateTime.UtcNow
            };

            return new AccountDeactivationResult(true, data, null, null, metadata);
        }

        public static AccountDeactivationResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static AccountDeactivationResult AlreadyInactive()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION, "User is already inactive");

        public static AccountDeactivationResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
}
