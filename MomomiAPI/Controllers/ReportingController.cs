using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            var reportResult = await _reportingService.ReportUserAsync(userIdResult.Value, request.UserId, request.Reason, request.Description);

            if (!reportResult.Success)
                return BadRequest(new { message = "Failed to report user or user already reported" });

            return Ok(new { message = "User reported successfully. Thank you for helping keep our community safe." });
        }

        /// <summary>
        /// Get user's reports
        /// </summary>
        [HttpGet("my-reports")]
        public async Task<ActionResult> GetMyReports()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetMyReports));

            var reports = await _reportingService.GetUserReportsAsync(userIdResult.Value);
            return Ok(new { data = reports });
        }

    }
}