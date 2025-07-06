using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.Enums;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MatchingController : ControllerBase
    {
        private readonly IMatchingService _matchingService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<MatchingController> _logger;

        public MatchingController(
            IMatchingService matchingService,
            ISubscriptionService subscriptionService,
            ILogger<MatchingController> logger)
        {
            _matchingService = matchingService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Get users for discovery/swiping
        /// </summary>
        [HttpGet("discovery")]
        public async Task<IActionResult> GetDiscoveryUsers([FromQuery] int count = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var discoveryUsers = await _matchingService.GetDiscoveryUsersAsync(userId.Value, count);
                return Ok(discoveryUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery users");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Like a user
        /// </summary>
        [HttpPost("like/{userId}")]
        public async Task<IActionResult> LikeUser([FromBody] LikeUserRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized();

                if (currentUserId == request.UserId)
                    return BadRequest(new { message = "You cannot like yourself" });

                // Check if user can like based on subscription
                var canLike = await _subscriptionService.CanUserLikeAsync(currentUserId.Value, request.LikeType);
                if (!canLike)
                {
                    var usageLimits = await _subscriptionService.GetUsageLimitsAsync(currentUserId.Value);
                    var limitType = request.LikeType == LikeType.SuperLike ? "super likes" : "likes";
                    return BadRequest(new
                    {
                        message = $"You've reached your daily {limitType} limit.",
                        usageLimits = usageLimits
                    });
                }

                var success = await _matchingService.LikeUserAsync(currentUserId.Value, request.UserId);

                if (!success)
                    return BadRequest(new { message = "User already processed or not found" });

                var updatedLimits = await _subscriptionService.GetUsageLimitsAsync(currentUserId.Value);
                var likeTypeText = request.LikeType == LikeType.SuperLike ? "Super liked" : "Liked";

                return Ok(new
                {
                    message = $"{likeTypeText} user successfully",
                    usageLimits = updatedLimits
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking user {UserId}", request.UserId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Pass on a user
        /// </summary>
        [HttpPost("pass/{userId}")]
        public async Task<IActionResult> PassUser(Guid userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized();

                if (currentUserId == userId)
                    return BadRequest(new { message = "You cannot pass on yourself" });

                var success = await _matchingService.PassUserAsync(currentUserId.Value, userId);

                if (!success)
                    return BadRequest(new { message = "User already processed or not found" });

                return Ok(new { message = "User passed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error passing user {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get current user's matches
        /// </summary>
        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var matches = await _matchingService.GetUserMatchesAsync(userId.Value);
                return Ok(matches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user matches");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Unmatch with a user
        /// </summary>
        [HttpDelete("unmatch/{userId}")]
        public async Task<IActionResult> UnmatchUser(Guid userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized();

                var success = await _matchingService.UnmatchAsync(currentUserId.Value, userId);

                if (!success)
                    return BadRequest(new { message = "Match not found" });

                return Ok(new { message = "Unmatched successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmatching user {UserId}", userId);
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
