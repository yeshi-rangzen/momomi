using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportingController : BaseApiController
    {
        private readonly IReportingService _reportingService;

        public ReportingController(IReportingService reportingService, ILogger<ReportingController> logger)
            : base(logger)
        {
            _reportingService = reportingService;
        }

        /// <summary>
        /// Report a user for policy violations
        /// </summary>
        [HttpPost("report")]
        public async Task<ActionResult<OperationResult<UserReportData>>> ReportUser([FromBody] ReportUserRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(ReportUser), new
            {
                reportedUserId = request.ReportedUserId,
                reason = request.Reason,
                hasDescription = !string.IsNullOrEmpty(request.Description)
            });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _reportingService.ReportUserAsync(
                userIdResult.Value,
                request.ReportedUserId,
                request.Reason,
                request.Description);

            return HandleOperationResult(result);
        }

        /// <summary>
        /// Block a user and remove all interactions
        /// </summary>
        [HttpPost("block")]
        public async Task<ActionResult<OperationResult<BlockUserData>>> BlockUser([FromBody] BlockUserRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(BlockUser), new { blockedUserId = request.BlockedUserId });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _reportingService.BlockUserAsync(userIdResult.Value, request.BlockedUserId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Unblock a previously blocked user
        /// </summary>
        [HttpPost("unblock")]
        public async Task<ActionResult<OperationResult<UnblockUserData>>> UnblockUser([FromBody] UnblockUserRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UnblockUser), new { unblockedUserId = request.UnblockedUserId });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _reportingService.UnblockUserAsync(userIdResult.Value, request.UnblockedUserId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get current user's submitted reports
        /// </summary>
        [HttpGet("reports")]
        public async Task<ActionResult<OperationResult<UserReportsListData>>> GetUserReports([FromQuery] GetReportsRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUserReports), new { page = request.Page, pageSize = request.PageSize });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _reportingService.GetUserReportsAsync(userIdResult.Value, request.Page, request.PageSize);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get current user's blocked users list
        /// </summary>
        [HttpGet("blocked-users")]
        public async Task<ActionResult<OperationResult<BlockedUsersListData>>> GetBlockedUsers([FromQuery] GetBlockedUsersRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetBlockedUsers), new { page = request.Page, pageSize = request.PageSize });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _reportingService.GetBlockedUsersAsync(userIdResult.Value, request.Page, request.PageSize);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Check if a specific user is blocked (for client-side checks)
        /// </summary>
        [HttpGet("blocked-status/{userId}")]
        public async Task<ActionResult<bool>> CheckBlockedStatus(Guid userId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(CheckBlockedStatus), new { checkedUserId = userId });

            var isBlocked = await _reportingService.IsUserBlockedAsync(userIdResult.Value, userId);
            return Ok(isBlocked);
        }

        /// <summary>
        /// Quick block action (convenience endpoint for mobile apps)
        /// </summary>
        [HttpPut("quick-block/{userId}")]
        public async Task<ActionResult<OperationResult<BlockUserData>>> QuickBlock(Guid userId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(QuickBlock), new { blockedUserId = userId });

            var result = await _reportingService.BlockUserAsync(userIdResult.Value, userId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Quick unblock action (convenience endpoint for mobile apps)
        /// </summary>
        [HttpPut("quick-unblock/{userId}")]
        public async Task<ActionResult<OperationResult<UnblockUserData>>> QuickUnblock(Guid userId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(QuickUnblock), new { unblockedUserId = userId });

            var result = await _reportingService.UnblockUserAsync(userIdResult.Value, userId);
            return HandleOperationResult(result);
        }
    }
}