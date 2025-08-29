using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
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
        public async Task<ActionResult<OperationResult<DiscoveryData>>> DiscoverUsersForSwiping([FromQuery] int count = 30)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DiscoverUsersForSwiping), new { count });

            var result = await _userDiscoveryService.DiscoverCandidatesAsync(userIdResult.Value, count);
            return HandleOperationResult(result);
        }

    }
}