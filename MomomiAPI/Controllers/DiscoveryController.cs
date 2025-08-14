using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class DiscoveryController : BaseApiController
    {
        private readonly IDiscoveryService _userDiscoveryService;

        public DiscoveryController(
            IDiscoveryService userDiscoveryService,
            ILogger<DiscoveryController> logger) : base(logger)
        {
            _userDiscoveryService = userDiscoveryService;
        }

        /// <summary>
        /// Discover users for swiping based on user preferences and subscription type
        /// </summary>
        [HttpGet("users")]
        public async Task<ActionResult<List<DiscoveryUserDTO>>> DiscoverUsersForSwiping([FromQuery] int count = 30)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DiscoverUsersForSwiping), new { count });

            var result = await _userDiscoveryService.DiscoverCandidatesAsync(userIdResult.Value, count);
            return HandleAuthResult(result);
        }

    }
}