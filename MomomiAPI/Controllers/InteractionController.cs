using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class InteractionsController : BaseApiController
    {
        private readonly IUserInteractionService _userInteractionService;

        public InteractionsController(
            IUserInteractionService userInteractionService,
            ILogger<InteractionsController> logger) : base(logger)
        {
            _userInteractionService = userInteractionService;
        }

        /// <summary>
        /// Express interest in another user (like or super like)
        /// </summary>
        [HttpPost("users/{userId}/like")]
        public async Task<ActionResult> ExpressInterestInUser(Guid userId, [FromBody] LikeUserRequest request)
        {
            var currentUserIdResult = GetCurrentUserIdOrUnauthorized();
            if (currentUserIdResult.Result != null) return currentUserIdResult.Result; // returns HTTP Response

            if (currentUserIdResult.Value == userId)
                return BadRequest(new { message = "You cannot like yourself" });

            LogControllerAction(nameof(ExpressInterestInUser), new { targetUserId = userId, likeType = request.LikeType });

            var result = await _userInteractionService.ExpressInterest(currentUserIdResult.Value, userId, request.LikeType);
            return HandleOperationResult(result);
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
            return HandleOperationResult(result);
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
            return HandleOperationResult(result);
        }
    }
}