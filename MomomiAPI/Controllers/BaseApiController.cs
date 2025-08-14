using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using System.Security.Claims;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger _logger;

        protected BaseApiController(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the current user's ID from the JWT token
        /// </summary>
        protected Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        /// <summary>
        /// Gets the current user's ID and returns Unauthorized if not found
        /// </summary>
        protected ActionResult<Guid> GetCurrentUserIdOrUnauthorized()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                var errorResult = OperationResult.Failed(
                                    ErrorCodes.UNAUTHORIZED,
                                    "Invalid or missing user authentication");

                return Unauthorized(errorResult);
            }
            return userId.Value;
        }

        /// <summary>
        /// Handles OperationResult and converts to appropriate HTTP response
        /// </summary>
        protected ActionResult HandleOperationResult(OperationResult result)
        {
            // Set timestamp for API response
            result.Timestamp = DateTime.UtcNow;

            if (result.Success)
            {
                return Ok(result);
            }

            LogApiError(result.ErrorCode, result.ErrorMessage);
            return ConvertErrorCodeToHttpResponse(result);
        }

        /// <summary>
        /// Handles OperationResult<T> and converts to appropriate HTTP response
        /// </summary>
        protected ActionResult<OperationResult<T>> HandleOperationResult<T>(OperationResult<T> result)
        {
            // Set timestamp for API response
            result.Timestamp = DateTime.UtcNow;

            if (result.Success)
            {
                return Ok(result);
            }

            LogApiError(result.ErrorCode, result.ErrorMessage);
            return ConvertErrorCodeToHttpResponse(result);
        }

        /// <summary>
        /// Logs controller action with context
        /// </summary>
        protected void LogControllerAction(string action, object? parameters = null)
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Controller action {Action} called by user {UserId} with parameters {@Parameters}",
                action, userId, parameters);
        }

        /// <summary>
        /// Logs API errors for monitoring and debugging
        /// </summary>
        private void LogApiError(string? errorCode, string? errorMessage)
        {
            var userId = GetCurrentUserId();
            _logger.LogWarning("API Error for user {UserId}: {ErrorCode} - {Message}",
                userId, errorCode, errorMessage);
        }

        /// <summary>
        /// Converts error codes to appropriate HTTP status codes and responses
        /// </summary>
        private ActionResult ConvertErrorCodeToHttpResponse(OperationResult result)
        {
            return result.ErrorCode switch
            {
                // Authentication & Authorization (401)
                ErrorCodes.UNAUTHORIZED or
                ErrorCodes.INVALID_CREDENTIALS or
                ErrorCodes.TOKEN_EXPIRED => Unauthorized(result),

                // Forbidden (403)
                ErrorCodes.FORBIDDEN => Forbid(),

                // Not Found (404)
                ErrorCodes.NOT_FOUND or
                ErrorCodes.USER_NOT_FOUND or
                ErrorCodes.RESOURCE_NOT_FOUND => NotFound(result),

                // Bad Request (400) - Validation and Business Logic
                ErrorCodes.VALIDATION_ERROR or
                ErrorCodes.INVALID_INPUT or
                ErrorCodes.REQUIRED_FIELD_MISSING or
                ErrorCodes.BUSINESS_RULE_VIOLATION or
                ErrorCodes.OPERATION_NOT_ALLOWED or
                ErrorCodes.USER_ALREADY_PROCESSED or
                ErrorCodes.USER_BLOCKED or
                ErrorCodes.LIKE_LIMIT_REACHED or
                ErrorCodes.SUPER_LIKE_LIMIT_REACHED or
                ErrorCodes.NO_RECENT_PASS_TO_UNDO => BadRequest(result),

                // Too Many Requests (429)
                ErrorCodes.RATE_LIMIT_EXCEEDED => StatusCode(429, result),

                // Internal Server Error (500)
                ErrorCodes.INTERNAL_SERVER_ERROR or
                ErrorCodes.DATABASE_ERROR or
                ErrorCodes.EXTERNAL_SERVICE_ERROR => StatusCode(500, result),

                // Default to 500 for unknown error codes
                _ => StatusCode(500, result)
            };
        }

        /// <summary>
        /// Helper method to handle exceptions and convert to consistent error response
        /// </summary>
        protected ActionResult HandleException(Exception ex, string userMessage = "An error occurred")
        {
            var userId = GetCurrentUserId();
            _logger.LogError(ex, "Unhandled exception in controller for user {UserId}", userId);

            var errorResult = OperationResult.Failed(
                ErrorCodes.INTERNAL_SERVER_ERROR,
                userMessage);

            return StatusCode(500, errorResult);
        }

        /// <summary>
        /// Helper method to create validation error responses
        /// </summary>
        protected ActionResult ValidationError(string message)
        {
            var errorResult = OperationResult.Failed(
                ErrorCodes.VALIDATION_ERROR,
                message);

            return BadRequest(errorResult);
        }

        /// <summary>
        /// Helper method to create not found responses
        /// </summary>
        protected ActionResult NotFoundError(string message = "Resource not found")
        {
            var errorResult = OperationResult.Failed(
                ErrorCodes.NOT_FOUND,
                message);

            return NotFound(errorResult);
        }
    }
}