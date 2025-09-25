using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class SwipeController : BaseApiController
    {
        private readonly ISwipeService _swipeService;
        private readonly IUserService _userService;
        public SwipeController(
            ISwipeService swipeService,
            IUserService userService,
            ILogger<SwipeController> logger) : base(logger)
        {
            _swipeService = swipeService;
            _userService = userService;
        }

        /// <summary>
        /// Express interest in another user (like or super like)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SwipeResult>> Swipe([FromBody] SwipeRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            if (userIdResult.Value == request.UserId)
                return BadRequest(new { message = "You cannot like yourself" });

            LogControllerAction(nameof(Swipe), new { swipeType = request.SwipeType });

            return await _swipeService.Swipe(userIdResult.Value, request.UserId, request.SwipeType);
        }

        [HttpPost("rewarded")]
        public async Task<ActionResult<SwipeResult>> RewardedSwipe([FromBody] SwipeRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            if (userIdResult.Value == request.UserId)
                return BadRequest(new { message = "You cannot like yourself" });

            LogControllerAction(nameof(RewardedSwipe), new { swipeType = request.SwipeType });

            return await _swipeService.RewardedSwipe(userIdResult.Value, request.UserId, request.SwipeType);
        }

        [HttpPost("undo")]
        public async Task<ActionResult<SwipeResult>> UndoLastSwipe()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UndoLastSwipe));

            var result = await _swipeService.UndoSwipe(userIdResult.Value);
            return result;
        }
    }
}