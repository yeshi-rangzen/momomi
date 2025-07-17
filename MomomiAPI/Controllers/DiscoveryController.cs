using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class DiscoveryController : BaseApiController
    {
        private readonly IUserDiscoveryService _userDiscoveryService;

        public DiscoveryController(
            IUserDiscoveryService userDiscoveryService,
            ILogger<DiscoveryController> logger) : base(logger)
        {
            _userDiscoveryService = userDiscoveryService;
        }

        /// <summary>
        /// Discover users for swiping based on user preferences
        /// </summary>
        [HttpGet("users")]
        public async Task<ActionResult<List<UserProfileDTO>>> DiscoverUsersForSwiping([FromQuery] int count = 10)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DiscoverUsersForSwiping), new { count });

            var result = await _userDiscoveryService.FindUsersForSwiping(userIdResult.Value, count);
            return HandleAuthResult(result); // Why use handleAuthResult?
        }

        /// <summary>
        /// TODO: Possible deletion as this can be handled in DisoverUsersForSwiping
        /// Discover users globally (ignoring location)
        /// </summary>
        [HttpGet("users/global")]
        public async Task<ActionResult<List<UserProfileDTO>>> DiscoverUsersGlobally([FromQuery] int count = 10)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DiscoverUsersGlobally), new { count });

            var result = await _userDiscoveryService.FindUsersGlobally(userIdResult.Value, count);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// TODO: Possible deletion as this can be handled in DisoverUsersForSwiping
        /// Discover users locally within specified distance
        /// </summary>
        [HttpGet("users/local")]
        public async Task<ActionResult<List<UserProfileDTO>>> DiscoverUsersLocally([FromQuery] int count = 10, [FromQuery] int maxDistance = 50)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DiscoverUsersLocally), new { count, maxDistance });

            var result = await _userDiscoveryService.FindUsersLocally(userIdResult.Value, count, maxDistance);
            return HandleOperationResult(result);
        }
    }
}