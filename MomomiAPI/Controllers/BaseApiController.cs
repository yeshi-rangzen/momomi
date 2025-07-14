using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using System.Security.Claims;

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
                return Unauthorized(new { message = "Invalid or missing user authentication" });
            }
            return userId.Value;
        }

        /// <summary>
        /// Converts OperationResult to appropriate HTTP response
        /// </summary>
        protected ActionResult HandleOperationResult(OperationResult result)
        {
            if (result.Success)
            {
                return Ok(new { message = "Operation completed successfully", metadata = result.Metadata });
            }

            return result.ErrorCode switch
            {
                "VALIDATION_ERROR" => BadRequest(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "BUSINESS_RULE_VIOLATION" => BadRequest(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "NOT_FOUND" => NotFound(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "UNAUTHORIZED" => Unauthorized(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                _ => StatusCode(500, new { message = result.ErrorMessage ?? "An error occurred", errorCode = result.ErrorCode })
            };
        }

        /// <summary>
        /// Converts OperationResult<T> to appropriate HTTP response
        /// </summary>
        protected ActionResult<T> HandleOperationResult<T>(OperationResult<T> result)
        {
            if (result.Success)
            {
                return Ok(new { data = result.Data, metadata = result.Metadata });
            }

            return result.ErrorCode switch
            {
                "VALIDATION_ERROR" => BadRequest(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "BUSINESS_RULE_VIOLATION" => BadRequest(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "NOT_FOUND" => NotFound(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                "UNAUTHORIZED" => Unauthorized(new { message = result.ErrorMessage, errorCode = result.ErrorCode }),
                _ => StatusCode(500, new { message = result.ErrorMessage ?? "An error occurred", errorCode = result.ErrorCode })
            };
        }

        /// <summary>
        /// Handles API responses for authentication results
        /// </summary>
        protected ActionResult HandleAuthResult<T>(T result) where T : OperationResult
        {
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode switch
            {
                "VALIDATION_ERROR" => BadRequest(result),
                "BUSINESS_RULE_VIOLATION" => BadRequest(result),
                "NOT_FOUND" => NotFound(result),
                "UNAUTHORIZED" => Unauthorized(result),
                _ => StatusCode(500, result)
            };
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
    }
}