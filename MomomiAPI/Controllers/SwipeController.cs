using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.Enums;
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
        public async Task<ActionResult<SwipeResult>> SwipeUser([FromBody] SwipeRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            if (userIdResult.Value == request.UserId)
                return BadRequest(new { message = "You cannot like yourself" });

            LogControllerAction(nameof(SwipeUser), new { swipeType = request.SwipeType });

            switch (request.SwipeType)
            {
                case SwipeType.Like:
                    return await _swipeService.LikeUser(userIdResult.Value, request.UserId);
                case SwipeType.SuperLike:
                    return await _swipeService.SuperLikeUser(userIdResult.Value, request.UserId);
                case SwipeType.Pass:
                    return await _swipeService.PassUser(userIdResult.Value, request.UserId);
                default:
                    break;
            }
            return BadRequest(new { message = $"{nameof(SwipeUser)}" });
        }


        [HttpPost("undo")]
        public async Task<ActionResult<SwipeResult>> UndoLastSwipe()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UndoLastSwipe));

            var result = await _swipeService.UndoLastSwipe(userIdResult.Value);
            return result;
        }
    }
}