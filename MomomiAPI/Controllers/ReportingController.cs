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
        public async Task<ActionResult<OperationResult>> ReportUser([FromBody] ReportUserRequest request)
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

            request.ReporterUserId = userIdResult.Value;

            var result = await _reportingService.ReportUserAsync(request);

            return HandleOperationResult(result);
        }

        /// <summary>
        /// Block a user and remove all interactions
        /// </summary>
        [HttpPost("block")]
        public async Task<ActionResult<OperationResult>> BlockUser([FromBody] BlockUserRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(BlockUser), new { blockedUserId = request.BlockedUserId });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            request.BlockerUserId = userIdResult.Value;

            var result = await _reportingService.BlockUserAsync(request);
            return HandleOperationResult(result);
        }

    }
}