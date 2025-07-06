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
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's subscription status
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var status = await _subscriptionService.GetUserSubscriptionAsync(userId.Value);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription status");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get current user's usage limits
        /// </summary>
        [HttpGet("usage-limits")]
        public async Task<IActionResult> GetUsageLimits()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var limits = await _subscriptionService.GetUsageLimitsAsync(userId.Value);
                return Ok(limits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage limits");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Upgrade to premium subscription
        /// </summary>
        [HttpPost("upgrade")]
        public async Task<IActionResult> UpgradeToPremium([FromBody] SubscriptionUpgradeRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // In a real app, you would process payment here using Stripe, PayPal, etc.
                // For MVP, we'll just upgrade directly
                var success = await _subscriptionService.UpgradeToPremiumAsync(userId.Value, request.DurationMonths);

                if (!success)
                    return BadRequest(new { message = "Failed to upgrade subscription" });

                var newStatus = await _subscriptionService.GetUserSubscriptionAsync(userId.Value);
                return Ok(new { message = "Successfully upgraded to Premium!", subscription = newStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upgrading to premium");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Cancel premium subscription
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var success = await _subscriptionService.CancelSubscriptionAsync(userId.Value);

                if (!success)
                    return BadRequest(new { message = "Failed to cancel subscription" });

                return Ok(new { message = "Subscription cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling subscription");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Record ad watched (for free users to earn bonus likes)
        /// </summary>
        [HttpPost("watch-ad")]
        public async Task<IActionResult> WatchAd()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var success = await _subscriptionService.RecordAdWatchedAsync(userId.Value);

                if (!success)
                    return BadRequest(new { message = "Unable to record ad watch. Daily limit reached or premium user." });

                var updatedLimits = await _subscriptionService.GetUsageLimitsAsync(userId.Value);
                return Ok(new
                {
                    message = "Ad watched! You earned a bonus like.",
                    usageLimits = updatedLimits
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording ad watch");
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