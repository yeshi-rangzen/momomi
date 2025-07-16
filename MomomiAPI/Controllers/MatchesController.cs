using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class MatchesController : BaseApiController
    {
        private readonly IMatchManagementService _matchManagementService;

        public MatchesController(
            IMatchManagementService matchManagementService,
            ILogger<MatchesController> logger) : base(logger)
        {
            _matchManagementService = matchManagementService;
        }

        /// <summary>
        /// Get current user's matches
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<MatchDTO>>> GetUserMatches()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUserMatches));

            var result = await _matchManagementService.GetUserMatches(userIdResult.Value);
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

            var result = await _matchManagementService.RemoveMatch(currentUserIdResult.Value, userId);
            return HandleOperationResult(result);
        }
    }
}