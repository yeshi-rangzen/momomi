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
    public class NotificationsController : BaseApiController
    {
        private readonly IPushNotificationService _pushNotificationService;

        public NotificationsController(IPushNotificationService pushNotificationService, ILogger<NotificationsController> logger)
            : base(logger)
        {
            _pushNotificationService = pushNotificationService;
        }

        /// <summary>
        /// Get user notifications
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<NotificationDTO>>> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetNotifications), new { page, pageSize });


            var result = await _pushNotificationService.GetUserNotificationsAsync(userIdResult.Value, page, pageSize);

            if (!result.Success)
            {
                return HandleOperationResult(result);
            }

            var unreadCountResult = await _pushNotificationService.GetUnreadNotificationCountAsync(userIdResult.Value);
            var unreadCount = unreadCountResult.Success ? unreadCountResult.Data : 0;

            return Ok(new
            {
                data = result.Data,
                unreadCount,
                page,
                pageSize,
                metadata = result.Metadata
            });
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUnreadCount));

            var result = await _pushNotificationService.GetUnreadNotificationCountAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        [HttpPut("{notificationId}/read")]
        public async Task<ActionResult> MarkAsRead(Guid notificationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(MarkAsRead), new { notificationId });

            var result = await _pushNotificationService.MarkNotificationAsReadAsync(notificationId, userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPut("mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(MarkAllAsRead));

            var result = await _pushNotificationService.MarkAllNotificationsAsReadAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update push token
        /// </summary>
        [HttpPut("push-token")]
        public async Task<IActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdatePushToken));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _pushNotificationService.UpdateUserPushTokenAsync(userIdResult.Value, request.PushToken);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Update notification settings
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UpdateNotificationSettings), new { request.NotificationsEnabled });

            var result = await _pushNotificationService.EnableNotificationsAsync(userIdResult.Value, request.NotificationsEnabled);
            return HandleOperationResult(result);
        }

    }
}