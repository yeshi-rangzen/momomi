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

    }
}