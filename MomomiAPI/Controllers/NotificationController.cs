using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(IPushNotificationService pushNotificationService, ILogger<NotificationsController> logger)
        {
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        /// <summary>
        /// Get user notifications
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var notifications = await _pushNotificationService.GetUserNotificationsAsync(userId.Value, page, pageSize);
                var unreadCount = await _pushNotificationService.GetUnreadNotificationCountAsync(userId.Value);

                return Ok(new { notifications, unreadCount, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var count = await _pushNotificationService.GetUnreadNotificationCountAsync(userId.Value);
                return Ok(new { unreadCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var success = await _pushNotificationService.MarkNotificationAsReadAsync(notificationId, userId.Value);

                if (!success)
                    return NotFound(new { message = "Notification not found" });

                return Ok(new { message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPut("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var success = await _pushNotificationService.MarkAllNotificationsAsReadAsync(userId.Value);

                if (!success)
                    return BadRequest(new { message = "Failed to mark notifications as read" });

                return Ok(new { message = "All notifications marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update push token
        /// </summary>
        [HttpPut("push-token")]
        public async Task<IActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var success = await _pushNotificationService.UpdateUserPushTokenAsync(userId.Value, request.PushToken);

                if (!success)
                    return BadRequest(new { message = "Failed to update push token" });

                return Ok(new { message = "Push token updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating push token");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update notification settings
        /// </summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return Unauthorized();

                var success = await _pushNotificationService.EnableNotificationsAsync(userId.Value, request.NotificationsEnabled);

                if (!success)
                    return BadRequest(new { message = "Failed to update notification settings" });

                return Ok(new
                {
                    message = request.NotificationsEnabled ? "Notifications enabled" : "Notifications disabled",
                    notificationsEnabled = request.NotificationsEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}