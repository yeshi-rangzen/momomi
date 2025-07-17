using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.DTOs;
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

        public ReportingController(IReportingService reportingService, ILogger<ReportingController> logger) : base(logger)
        {
            _reportingService = reportingService;
        }

        /// <summary>
        /// Report a user
        /// </summary>
        [HttpPost("report")]
        public async Task<ActionResult> ReportUser([FromBody] ReportUserRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(ReportUser), new { request.UserId, request.Reason });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (userIdResult.Value == request.UserId)
                return BadRequest(new { message = "You cannot report yourself" });

            var result = await _reportingService.ReportUserAsync(userIdResult.Value, request.UserId, request.Reason, request.Description);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get user's reports
        /// </summary>
        [HttpGet("my-reports")]
        public async Task<ActionResult<List<UserReportDTO>>> GetMyReports()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetMyReports));

            var result = await _reportingService.GetUserReportsAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

    }
}