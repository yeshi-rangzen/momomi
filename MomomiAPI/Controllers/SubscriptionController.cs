using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;
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
        private readonly IAnalyticsService _analyticsService;

        public SubscriptionController(
            ISubscriptionService subscriptionService,
            IAnalyticsService analyticsService,
            ILogger<SubscriptionController> logger) : base(logger)
        {
            _subscriptionService = subscriptionService;
            _analyticsService = analyticsService;
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

            // Track subscription upgrade
            if (result.Success)
            {
                _ = Task.Run(() =>
                {
                    var analyticsData = new SubscriptionData
                    {
                        PlanType = SubscriptionType.Premium,
                        DurationMonths = request.DurationMonths,
                        PricePaid = CalculatePrice(request.DurationMonths),
                        PaymentMethod = request.PaymentToken ?? "unknown",
                        TriggerReason = DetermineTriggerReason(userIdResult.Value), // TODO: Implement
                        PreviousSubscription = SubscriptionType.Free,
                        UpgradeTimestamp = DateTime.UtcNow
                    };

                    return _analyticsService.TrackSubscriptionUpgradedAsync(userIdResult.Value, analyticsData);
                });
            }
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

            // Get current subscription before cancelling
            var currentSubscriptionResult = await _subscriptionService.GetUserSubscriptionAsync(userIdResult.Value);

            var result = await _subscriptionService.CancelSubscriptionAsync(userIdResult.Value);

            // Track subscription cancellation
            if (result.Success && currentSubscriptionResult.Success && currentSubscriptionResult.Data != null)
            {
                _ = Task.Run(() =>
                {
                    var daysSubscribed = currentSubscriptionResult.Data.StartsAt != null ?
                        (int)(DateTime.UtcNow - currentSubscriptionResult.Data.StartsAt).TotalDays : 0;

                    var analyticsData = new CancellationData
                    {
                        CancellationReason = "user_requested", // TODO: Get from request
                        DaysSubscribed = daysSubscribed,
                        CancelledPlan = currentSubscriptionResult.Data.SubscriptionType,
                        CancellationTimestamp = DateTime.UtcNow
                    };

                    return _analyticsService.TrackSubscriptionCancelledAsync(userIdResult.Value, analyticsData);
                });
            }
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

        private static decimal CalculatePrice(int durationMonths)
        {
            // TODO: Implement actual pricing logic
            return durationMonths * 9.99m; // Example: $9.99 per month
        }

        private static string DetermineTriggerReason(Guid userId)
        {
            // TODO: Implement logic to determine why user upgraded
            // Could check recent limit hits, feature usage, etc.
            return "unknown";
        }
    }
}