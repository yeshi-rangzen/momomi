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
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's profile
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var profile = await _userService.GetUserProfileAsync(userId.Value);
                if (profile == null)
                    return NotFound();

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update current user's profile
        /// </summary> 
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var success = await _userService.UpdateUserProfileAsync(userId.Value, request);
                if (!success)
                    return BadRequest(new { message = "Failed to update profile" });

                var updatedProfile = await _userService.GetUserProfileAsync(userId.Value);
                return Ok(updatedProfile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Toggle global discovery mode
        /// </summary>
        [HttpPut("discovery-mode")]
        public async Task<IActionResult> UpdateDiscoveryMode([FromBody] DiscoveryModeRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var updateRequest = new UpdateProfileRequest
                {
                    EnableGlobalDiscovery = request.EnableGlobalDiscovery
                };

                var success = await _userService.UpdateUserProfileAsync(userId.Value, updateRequest);
                if (!success)
                    return BadRequest(new { message = "Failed to update discovery mode" });

                return Ok(new
                {
                    message = request.EnableGlobalDiscovery ?
                        "Global discovery enabled. You can now discover profiles worldwide." :
                        "Location-based discovery enabled. Showing profiles within your specified distance.",
                    enableGlobalDiscovery = request.EnableGlobalDiscovery
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discovery mode");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get nearby users for discovery
        /// </summary>
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyUsers([FromQuery] int maxDistance = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var nearbyUsers = await _userService.GetNearbyUsersAsync(userId.Value, maxDistance);
                return Ok(nearbyUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby users");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get current user's discovery settings
        /// </summary>
        [HttpGet("discovery-settings")]
        public async Task<IActionResult> GetDiscoverySettings()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                    return NotFound();

                return Ok(new
                {
                    enableGlobalDiscovery = user.EnableGlobalDiscovery,
                    maxDistanceKm = user.MaxDistanceKm,
                    minAge = user.MinAge,
                    maxAge = user.MaxAge,
                    hasLocation = user.Latitude.HasValue && user.Longitude.HasValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discovery settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


        /// <summary>
        /// Deactivate current user's account
        /// </summary>
        [HttpDelete("deactivate")]
        public async Task<IActionResult> DeactivateAccount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _userService.DeactivateUserAsync(userId.Value);
                if (!success)
                    return BadRequest(new { message = "Failed to deactivate account" });

                return Ok(new { message = "Account deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user account");
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
