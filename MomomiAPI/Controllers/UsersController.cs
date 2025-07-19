using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : BaseApiController
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService, ILogger<UsersController> logger) : base(logger)
        {
            _userService = userService;
        }

        /// <summary>
        /// Get current user's profile
        /// </summary>
        [HttpGet("profile")]
        public async Task<ActionResult<UserProfileDTO>> GetProfile()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetProfile));

            var result = await _userService.GetUserProfileAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update current user's profile
        /// </summary> 
        [HttpPut("profile")]
        public async Task<ActionResult<UserProfileDTO>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateProfile));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.UpdateUserProfileAsync(userIdResult.Value, request);
            if (!result.Success)
                return HandleOperationResult(result);

            // Get updated profile
            var updatedProfileResult = await _userService.GetUserProfileAsync(userIdResult.Value);
            return HandleOperationResult(updatedProfileResult);
        }

        /// <summary>
        /// Toggle global discovery mode
        /// </summary>
        [HttpPut("discovery-mode")]
        public async Task<ActionResult> UpdateDiscoveryMode([FromBody] DiscoveryModeRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateDiscoveryMode), new { request.EnableGlobalDiscovery });

            var updateRequest = new UpdateProfileRequest
            {
                EnableGlobalDiscovery = request.EnableGlobalDiscovery
            };

            var result = await _userService.UpdateUserProfileAsync(userIdResult.Value, updateRequest);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Toggle profile discovery visibility
        /// </summary>
        [HttpPut("discovery-visibility")]
        public async Task<ActionResult> UpdateDiscoveryVisibility([FromBody] UpdateDiscoveryVisibilityRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateDiscoveryVisibility), new { request.IsDiscoverable });

            var result = await _userService.UpdateDiscoveryStatusAsync(userIdResult.Value, request.IsDiscoverable);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get nearby users for discovery
        /// </summary>
        [HttpGet("nearby")]
        public async Task<ActionResult<List<UserProfileDTO>>> GetNearbyUsers([FromQuery] int maxDistance = 50)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetNearbyUsers), new { maxDistance });

            var result = await _userService.GetNearbyUsersAsync(userIdResult.Value, maxDistance);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get current user's discovery settings
        /// </summary>
        [HttpGet("discovery-settings")]
        public async Task<ActionResult<DiscoverySettingsDTO>> GetDiscoverySettings()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetDiscoverySettings));

            var result = await _userService.GetDiscoverySettingsAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update current user's discovery settings
        /// </summary>
        [HttpPut("discovery-settings")]
        public async Task<ActionResult<DiscoverySettingsDTO>> UpdateDiscoverySettings([FromBody] UpdateDiscoverySettingsRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateDiscoverySettings), new
            {
                request.EnableGlobalDiscovery,
                request.IsDiscoverable,
                request.MaxDistanceKm,
                request.MinAge,
                request.MaxAge
            });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.UpdateDiscoverySettingsAsync(userIdResult.Value, request);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Delete user account and all associated data
        /// </summary>
        [HttpDelete("delete-account")]
        public async Task<ActionResult> DeleteAccount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeleteAccount));

            var result = await _userService.DeleteUserAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Deactivate current user's account
        /// </summary>
        [HttpDelete("deactivate")]
        public async Task<ActionResult> DeactivateAccount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeactivateAccount));

            var result = await _userService.DeactivateUserAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }
    }
}
