using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    /// Discovery-specific data
    public class DiscoveryData
    {
        public List<DiscoveryUserDTO> Users { get; set; } = [];
        public bool HasMore { get; set; }
    }

    public class DiscoveryResult : OperationResult<DiscoveryData>
    {
        private DiscoveryResult(bool success, DiscoveryData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static DiscoveryResult Successful(List<DiscoveryUserDTO> users,
             bool hasMore, Dictionary<string, object>? metadata = null)
        {
            var discoveryData = new DiscoveryData
            {
                Users = users,
                HasMore = hasMore
            };

            return new DiscoveryResult(true, discoveryData, null, null, metadata);
        }

        public static DiscoveryResult NoUsersFound()
        {
            var discoveryData = new DiscoveryData
            {
                Users = [],
                HasMore = false
            };

            return new DiscoveryResult(true, discoveryData, null, null);
        }

        public static DiscoveryResult UserNotFound()
        {
            return new DiscoveryResult(false, null, ErrorCodes.USER_NOT_FOUND,
                "Current user not found");
        }

        public static DiscoveryResult LocationRequired()
        {
            return new DiscoveryResult(false, null, ErrorCodes.VALIDATION_ERROR,
                "Location is required for local discovery");
        }

        public static DiscoveryResult Error(string errorMessage)
        {
            return new DiscoveryResult(false, null, ErrorCodes.INTERNAL_SERVER_ERROR,
                errorMessage);
        }
    }
}