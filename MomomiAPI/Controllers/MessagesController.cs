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
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
        {
            _messageService = messageService;
            _logger = logger;
        }

        /// <summary>
        /// Get user's conversations
        /// </summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var conversations = await _messageService.GetUserConversationsAsync(userId.Value);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get specific conversation details
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<IActionResult> GetConversation(Guid conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var conversation = await _messageService.GetConversationAsync(userId.Value, conversationId);
                if (conversation == null)
                    return NotFound(new { message = "Conversation not found" });

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<IActionResult> GetConversationMessages(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var messages = await _messageService.GetConversationMessagesAsync(userId.Value, conversationId, page, pageSize);

                return Ok(new { messages, page, pageSize, hasMore = messages.Count == pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation messages");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var message = await _messageService.SendMessageAsync(userId.Value, request);
                if (message == null)
                    return BadRequest(new { message = "Failed to send message" });

                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPut("conversations/{conversationId}/read")]
        public async Task<IActionResult> MarkMessagesAsRead(Guid conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _messageService.MarkMessagesAsReadAsync(userId.Value, conversationId);
                if (!success)
                    return NotFound(new { message = "Failed to mark messages as read" });

                return Ok(new { message = "Messages marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessages(Guid messageId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _messageService.DeleteMessageAsync(userId.Value, messageId);
                if (!success)
                    return NotFound(new { message = "Message not found or permission denied" });

                return Ok(new { message = "Message deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
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
