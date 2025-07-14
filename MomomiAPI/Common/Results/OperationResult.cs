namespace MomomiAPI.Common.Results
{
    public class OperationResult
    {
        public bool Success { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public string? ErrorCode { get; protected set; }
        public Dictionary<string, object>? Metadata { get; protected set; }

        protected OperationResult(bool success, string? errorMessage = null, string? errorCode = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Metadata = new Dictionary<string, object>();
        }

        public static OperationResult Successful() => new(true);
        public static OperationResult Failed(string errorMessage, string? errorCode = null)
            => new(false, errorMessage, errorCode);
        public static OperationResult ValidationFailure(string message)
            => new(false, message, "VALIDATION_ERROR");
        public static OperationResult BusinessRuleViolation(string message)
            => new(false, message, "BUSINESS_RULE_VIOLATION");
        public static OperationResult NotFound(string message)
            => new(false, message, "NOT_FOUND");
        public static OperationResult Unauthorized(string message)
            => new(false, message, "UNAUTHORIZED");

        public OperationResult WithMetadata(string key, object value)
        {
            Metadata ??= new Dictionary<string, object>();
            Metadata[key] = value;
            return this;
        }
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; private set; }

        public OperationResult(bool success, T? data = default, string? errorMessage = null, string? errorCode = null)
            : base(success, errorMessage, errorCode)
        {
            Data = data;
        }

        public static OperationResult<T> Successful(T data) => new(true, data);
        public static new OperationResult<T> Failed(string errorMessage, string? errorCode = null)
            => new(false, default, errorMessage, errorCode);
        public static new OperationResult<T> ValidationFailure(string message)
            => new(false, default, message, "VALIDATION_ERROR");
        public static new OperationResult<T> BusinessRuleViolation(string message)
            => new(false, default, message, "BUSINESS_RULE_VIOLATION");
        public static new OperationResult<T> NotFound(string message)
            => new(false, default, message, "NOT_FOUND");
        public static new OperationResult<T> Unauthorized(string message)
            => new(false, default, message, "UNAUTHORIZED");

        public new OperationResult<T> WithMetadata(string key, object value)
        {
            base.WithMetadata(key, value);
            return this;
        }
    }
}