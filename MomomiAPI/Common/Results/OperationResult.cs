using System.Text.Json.Serialization;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    public class OperationResult
    {
        public bool Success { get; protected set; }
        public string? ErrorCode { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public Dictionary<string, object>? Metadata { get; protected set; }

        // API-specific properties (only populated at API layer)
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonIgnore] // Don't serialize for internal use
        public Dictionary<string, object>? InternalMetadata { get; set; }

        // Add constructor for derived classes
        protected OperationResult(bool success, string? errorCode = null, string? errorMessage = null, Dictionary<string, object>? metadata = null)
        {
            Success = success;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Metadata = metadata;
        }

        // Parameterless constructor for static factory methods
        protected OperationResult() { }

        public static OperationResult Successful(Dictionary<string, object>? metadata = null)
        {
            return new OperationResult(true, metadata: metadata);
        }

        public static OperationResult Failed(string errorCode, string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult(false, errorCode, errorMessage, metadata);
        }

        public static OperationResult Failed(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult(false, ErrorCodes.INTERNAL_SERVER_ERROR, errorMessage, metadata);
        }

        public static OperationResult ValidationFailure(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult(false, ErrorCodes.VALIDATION_ERROR, errorMessage, metadata);
        }
    }

    public class OperationResult<T> : OperationResult
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }

        // Add constructor for derived classes
        protected OperationResult(bool success, T? data, string? errorCode = null, string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, errorCode, errorMessage, metadata)
        {
            Data = data;
        }

        // Parameterless constructor for static factory methods
        protected OperationResult() { }

        public static OperationResult<T> Successful(T data, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult<T>(true, data, metadata: metadata);
        }
        public static OperationResult<T> SuccessResult(T data, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult<T>(true, data, metadata: metadata);
        }

        public static new OperationResult<T> FailureResult(string errorCode, string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult<T>(false, default(T), errorCode, errorMessage, metadata);

        }

        public static new OperationResult<T> Failed(string errorMessage, Dictionary<string, object>? metadata = null)
        {
            return new OperationResult<T>(false, default(T), ErrorCodes.INTERNAL_SERVER_ERROR, errorMessage, metadata);

        }
    }
}