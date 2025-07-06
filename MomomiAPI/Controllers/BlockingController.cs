using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BlockingController : ControllerBase
    {
        private readonly IBlockingService _blockingService;
        private readonly ILogger<BlockingController> _logger;

        public BlockingController(IBlockingService blockingService, ILogger<BlockingController> logger)
        {
            _blockingService = blockingService;
            _logger = logger;
        }

        /// <summary>
        /// Block a user
        /// </summary>
        [HttpPost("block")]
        public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (userId == request.UserId)
                    return BadRequest(new { message = "You cannot block yourself" });

                var success = await _blockingService.BlockUserAsync(userId.Value, request.UserId, request.Reason);

                if (!success)
                    return BadRequest(new { message = "Failed to block user" });

                return Ok(new { message = "User blocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Unblock a user
        /// </summary>
        [HttpPost("unblock/{userId}")]
        public async Task<IActionResult> UnblockUser(Guid userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null) return Unauthorized();

                var success = await _blockingService.UnblockUserAsync(currentUserId.Value, userId);

                if (!success)
                    return BadRequest(new { message = "Failed to unblock user" });

                return Ok(new { message = "User unblocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get list of blocked users
        /// </summary>
        [HttpGet("blocked-users")]
        public async Task<IActionResult> GetBlockedUsers()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var blockedUsers = await _blockingService.GetBlockedUsersAsync(userId.Value);
                return Ok(blockedUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blocked users");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Report a user
        /// </summary>
        [HttpPost("report")]
        public async Task<IActionResult> ReportUser([FromBody] ReportUserRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (userId == request.UserId)
                    return BadRequest(new { message = "You cannot report yourself" });

                var success = await _blockingService.ReportUserAsync(userId.Value, request.UserId, request.Reason, request.Description);

                if (!success)
                    return BadRequest(new { message = "Failed to report user or user already reported" });

                return Ok(new { message = "User reported successfully. Thank you for helping keep our community safe." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get user's reports
        /// </summary>
        [HttpGet("my-reports")]
        public async Task<IActionResult> GetMyReports()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var reports = await _blockingService.GetUserReportsAsync(userId.Value);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reports");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}