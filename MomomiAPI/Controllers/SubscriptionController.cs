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
    public class SubscriptionController : BaseApiController
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
            : base(logger)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// Get current user's subscription status
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<SubscriptionStatusDTO>> GetSubscriptionStatus()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetSubscriptionStatus));

            var result = await _subscriptionService.GetUserSubscriptionAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get current user's usage limits
        /// </summary>
        [HttpGet("usage-limits")]
        public async Task<ActionResult<UsageLimitsDTO>> GetUsageLimits()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUsageLimits));

            var result = await _subscriptionService.GetUsageLimitsAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Upgrade to premium subscription
        /// </summary>
        [HttpPost("upgrade")]
        public async Task<ActionResult> UpgradeToPremium([FromBody] SubscriptionUpgradeRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpgradeToPremium), new { request.DurationMonths });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _subscriptionService.UpgradeToPremiumAsync(userIdResult.Value, request.DurationMonths);

            if (!result.Success)
            {
                return HandleOperationResult(result);
            }

            // Get updated subscription status
            var statusResult = await _subscriptionService.GetUserSubscriptionAsync(userIdResult.Value);

            return Ok(new
            {
                message = "Successfully upgraded to Premium!",
                data = statusResult.Data,
                metadata = result.Metadata
            });
        }

        /// <summary>
        /// Cancel premium subscription
        /// </summary>
        [HttpPost("cancel")]
        public async Task<ActionResult> CancelSubscription()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(CancelSubscription));

            var result = await _subscriptionService.CancelSubscriptionAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Record ad watched (for free users to earn bonus likes)
        /// </summary>
        [HttpPost("watch-ad")]
        public async Task<ActionResult> WatchAd()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(WatchAd));

            var result = await _subscriptionService.RecordAdWatchedAsync(userIdResult.Value);

            if (!result.Success)
            {
                return HandleOperationResult(result);
            }

            // Get updated usage limits
            var limitsResult = await _subscriptionService.GetUsageLimitsAsync(userIdResult.Value);

            return Ok(new
            {
                message = "Ad watched! You earned a bonus like.",
                data = limitsResult.Data,
                metadata = result.Metadata
            });
        }
    }
}