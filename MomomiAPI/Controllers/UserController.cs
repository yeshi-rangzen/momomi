using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : BaseApiController
    {
        private readonly IUserService _userService;
        private readonly ICacheInvalidation _cacheInvalidationService;

        public UserController(IUserService userService, ILogger<UserController> logger, ICacheInvalidation cacheInvalidationService) : base(logger)
        {
            _userService = userService;
            _cacheInvalidationService = cacheInvalidationService;
        }

        /// <summary>
        /// Get current user's profile
        /// </summary>
        [HttpGet("profile")]
        public async Task<ActionResult<OperationResult<UserProfileData>>> GetProfile()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetProfile));

            var result = await _userService.GetUserProfileAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update current user's profile (non-discovery fields)
        /// </summary> 
        [HttpPut("profile")]
        public async Task<ActionResult<OperationResult<ProfileUpdateData>>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateProfile), new
            {
                hasFirstName = !string.IsNullOrEmpty(request.FirstName),
                hasLastName = !string.IsNullOrEmpty(request.LastName),
                hasBio = !string.IsNullOrEmpty(request.Bio),
                hasOccupation = !string.IsNullOrEmpty(request.Occupation),
                hasHeightCm = request.HeightCm.HasValue
            });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.UpdateUserProfileAsync(userIdResult.Value, request);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update current user's discovery filters and preferences
        /// Includes both free and premium filters with subscription validation
        /// </summary>
        [HttpPut("discovery-filters")]
        public async Task<ActionResult<OperationResult<DiscoveryFiltersUpdateData>>> UpdateDiscoveryFilters([FromBody] UpdateDiscoveryFiltersRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateDiscoveryFilters), new
            {
                hasInterestedIn = request.InterestedIn.HasValue,
                hasAgeRange = request.MinAge.HasValue || request.MaxAge.HasValue,
                hasLocation = request.Latitude.HasValue && request.Longitude.HasValue,
                hasMaxDistance = request.MaxDistanceKm.HasValue,
                hasDiscoverySettings = request.IsDiscoverable.HasValue || request.EnableGlobalDiscovery.HasValue,
                hasFreeFilters = request.PreferredHeritage != null || request.PreferredReligions != null || request.PreferredLanguagesSpoken != null,
                hasPremiumFilters = request.GetPremiumFiltersBeingUpdated().Any()
            });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userService.UpdateDiscoveryFiltersAsync(userIdResult.Value, request);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Deactivate current user's account (soft delete)
        /// User can potentially reactivate later
        /// </summary>
        [HttpPut("deactivate")]
        public async Task<ActionResult<OperationResult<AccountDeactivationData>>> DeactivateAccount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeactivateAccount));

            var result = await _userService.DeactivateUserAccountAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Permanently delete user account and all associated data
        /// This action cannot be undone
        /// </summary>
        [HttpDelete("delete")]
        public async Task<ActionResult<OperationResult<AccountDeletionData>>> DeleteAccount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeleteAccount));

            var result = await _userService.DeleteUserAccountAsync(userIdResult.Value);

            await _cacheInvalidationService.InvalidateOnLogoutOrDelete(userIdResult.Value);
            return HandleOperationResult(result);
        }

    }

}