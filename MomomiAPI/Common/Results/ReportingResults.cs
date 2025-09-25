using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    /// User report submission data

    public class UserReportResult : OperationResult
    {
        private UserReportResult(bool success, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, errorCode, errorMessage, metadata)
        {
        }

        public static UserReportResult AlreadyReported()
            => new(false, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "You have already reported this user");

        public static UserReportResult UserNotFound()
            => new(false, ErrorCodes.USER_NOT_FOUND,
                "User not found or inactive");

        public static UserReportResult CannotReportSelf()
            => new(false, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "You cannot report yourself");

        public static UserReportResult ValidationError(string message)
            => new(false, ErrorCodes.VALIDATION_ERROR, message);

        public static UserReportResult Error(string message)
            => new(false, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Block user data
    public class BlockUserData
    {
        public Guid BlockedUserId { get; set; }
        public bool MatchRemoved { get; set; }
        public bool ConversationDeleted { get; set; }
        public int MessagesDeleted { get; set; }
        public DateTime BlockedAt { get; set; }
    }

    public class BlockUserResult : OperationResult
    {
        private BlockUserResult(bool success, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, errorCode, errorMessage, metadata)
        {
        }

        public static BlockUserResult AlreadyBlocked()
            => new(false, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "You have already blocked this user");

        public static BlockUserResult UserNotFound()
            => new(false, ErrorCodes.USER_NOT_FOUND,
                "User not found or inactive");

        public static BlockUserResult CannotBlockSelf()
            => new(false, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "You cannot block yourself");

        public static BlockUserResult Error(string message)
            => new(false, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// User reports list data
    public class UserReportsListData
    {
        public List<UserReportDTO> Reports { get; set; } = new();
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public bool HasMore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class UserReportsListResult : OperationResult<UserReportsListData>
    {
        private UserReportsListResult(bool success, UserReportsListData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static UserReportsListResult Successful(List<UserReportDTO> reports, int totalCount,
            int pendingCount, bool hasMore, Dictionary<string, object>? metadata = null)
        {
            var data = new UserReportsListData
            {
                Reports = reports,
                TotalCount = totalCount,
                PendingCount = pendingCount,
                HasMore = hasMore,
                LastUpdated = DateTime.UtcNow
            };

            return new UserReportsListResult(true, data, null, null, metadata);
        }

        public static UserReportsListResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Blocked users list data
    public class BlockedUsersListData
    {
        public List<BlockedUserDTO> BlockedUsers { get; set; } = new();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class BlockedUsersListResult : OperationResult<BlockedUsersListData>
    {
        private BlockedUsersListResult(bool success, BlockedUsersListData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static BlockedUsersListResult Successful(List<BlockedUserDTO> blockedUsers, int totalCount,
            bool hasMore, Dictionary<string, object>? metadata = null)
        {
            var data = new BlockedUsersListData
            {
                BlockedUsers = blockedUsers,
                TotalCount = totalCount,
                HasMore = hasMore,
                LastUpdated = DateTime.UtcNow
            };

            return new BlockedUsersListResult(true, data, null, null, metadata);
        }

        public static BlockedUsersListResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Unblock user data
    public class UnblockUserData
    {
        public Guid UnblockedUserId { get; set; }
        public DateTime UnblockedAt { get; set; }
    }

    public class UnblockUserResult : OperationResult<UnblockUserData>
    {
        private UnblockUserResult(bool success, UnblockUserData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static UnblockUserResult Successful(Guid unblockedUserId, Dictionary<string, object>? metadata = null)
        {
            var data = new UnblockUserData
            {
                UnblockedUserId = unblockedUserId,
                UnblockedAt = DateTime.UtcNow
            };

            return new UnblockUserResult(true, data, null, null, metadata);
        }

        public static UnblockUserResult NotBlocked()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "User is not currently blocked");

        public static UnblockUserResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "User not found");

        public static UnblockUserResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
}