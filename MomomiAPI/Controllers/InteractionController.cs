using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class InteractionsController : BaseApiController
    {
        private readonly IUserInteractionService _userInteractionService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IUserService _userService;
        public InteractionsController(
            IUserInteractionService userInteractionService,
            IAnalyticsService analyticsService,
            IUserService userService,
            ILogger<InteractionsController> logger) : base(logger)
        {
            _userInteractionService = userInteractionService;
            _analyticsService = analyticsService;
            _userService = userService;
        }

        /// <summary>
        /// Express interest in another user (like or super like)
        /// </summary>
        [HttpPost("users/like")]
        public async Task<ActionResult> ExpressInterestInUser(Guid userId, [FromBody] LikeUserRequest request)
        {
            var currentUserIdResult = GetCurrentUserIdOrUnauthorized();
            if (currentUserIdResult.Result != null) return currentUserIdResult.Result; // returns HTTP Response

            if (currentUserIdResult.Value == userId)
                return BadRequest(new { message = "You cannot like yourself" });

            LogControllerAction(nameof(ExpressInterestInUser), new { targetUserId = userId, likeType = request.LikeType });

            var result = await _userInteractionService.ExpressInterest(currentUserIdResult.Value, userId, request.LikeType);

            // Track like interaction
            if (result.Success)
            {
                _ = Task.Run(async () =>
                {
                    // Get target user data for analytics
                    var targetUserResult = await _userService.GetUserByIdAsync(userId);

                    var analyticsData = new LikeInteractionData
                    {
                        LikeType = request.LikeType,
                        DiscoveryMode = targetUserResult.Data.EnableGlobalDiscovery.ToString(), // TODO: Pass from frontend
                        //CulturalCompatibilityScore = null, // TODO: Calculate if available
                        TargetHeritage = targetUserResult.Success ? targetUserResult.Data?.Heritage : null,
                        IsMatch = result.IsMatch,
                        InteractionTimestamp = DateTime.UtcNow
                    };

                    await _analyticsService.TrackLikeRecordedAsync(currentUserIdResult.Value, userId, analyticsData);
                });
            }

            return HandleInteractionResult(result);

        }

        /// <summary>
        /// Dismiss (pass on) another user
        /// </summary>
        [HttpPost("users/{userId}/pass")]
        public async Task<ActionResult> DismissUser(Guid userId)
        {
            var currentUserIdResult = GetCurrentUserIdOrUnauthorized();
            if (currentUserIdResult.Result != null) return currentUserIdResult.Result;

            if (currentUserIdResult.Value == userId)
                return BadRequest(new { message = "You cannot pass on yourself" });

            LogControllerAction(nameof(DismissUser), new { targetUserId = userId });

            var result = await _userInteractionService.DismissUser(currentUserIdResult.Value, userId);
            return HandleInteractionResult(result);
        }

        /// <summary>
        /// Undo the last swipe action
        /// </summary>
        [HttpPost("undo-last-swipe")]
        public async Task<ActionResult> UndoLastSwipe()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UndoLastSwipe));

            var result = await _userInteractionService.UndoLastSwipe(userIdResult.Value);
            return HandleInteractionResult(result);
        }

        /// <summary>
        /// Get users who have liked the current user
        /// </summary>
        [HttpGet("users-who-liked-me")]
        public async Task<ActionResult<List<UserLikeDTO>>> GetUsersWhoLikedMe([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUsersWhoLikedMe), new { page, pageSize });

            var result = await _userInteractionService.GetUsersWhoLikedMe(userIdResult.Value, page, pageSize);
            return HandleOperationResult(result);
        }
    }
}