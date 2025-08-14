using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class MatchesController : BaseApiController
    {
        private readonly IMatchService _matchService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IUserService _userService;

        public MatchesController(
            IMatchService matchService,
            IAnalyticsService analyticsService,
            IUserService userService,
            ILogger<MatchesController> logger) : base(logger)
        {
            _matchService = matchService;
            _analyticsService = analyticsService;
            _userService = userService;
        }

        /// <summary>
        /// Get current user's matches
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<OperationResult<MatchData>>> GetUserMatches()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUserMatches));

            var result = await _matchService.GetMatchConversations(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Remove a match (unmatch)
        /// </summary>
        [HttpDelete("{userId}")]
        public async Task<ActionResult> RemoveMatch(Guid userId)
        {
            var currentUserIdResult = GetCurrentUserIdOrUnauthorized();
            if (currentUserIdResult.Result != null) return currentUserIdResult.Result;

            LogControllerAction(nameof(RemoveMatch), new { matchedUserId = userId });

            var result = await _matchService.RemoveMatchConversation(currentUserIdResult.Value, userId, Models.Enums.SwipeType.Unmatched);
            return HandleOperationResult(result);
        }
    }
}