using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : BaseApiController
    {
        private readonly IMessageService _messageService;

        public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
            : base(logger)
        {
            _messageService = messageService;
        }

        /// <summary>
        /// Get user's conversations
        /// </summary>
        [HttpGet("conversations")]
        public async Task<ActionResult> GetConversations()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversations), new { userIdResult.Value });

            var conversationsResult = await _messageService.GetUserConversationsAsync(userIdResult.Value);
            if (!conversationsResult.Success)
                return BadRequest(new { message = "Failed to retrieve conversations." });

            return Ok(conversationsResult);

        }

        /// <summary>
        /// Get specific conversation details
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<ActionResult> GetConversation(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversation), new { userIdResult.Value });

            var conversationsResult = await _messageService.GetConversationAsync(userIdResult.Value, conversationId);
            if (!conversationsResult.Success)
                return BadRequest(new { message = "Failed retrieve conversation." });

            return Ok(conversationsResult);

        }

        /// <summary>
        /// Get messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<ActionResult> GetConversationMessages(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversationMessages), new { userIdResult.Value });

            var messagesResult = await _messageService.GetConversationMessagesAsync(userIdResult.Value, conversationId, page, pageSize);

            if (!messagesResult.Success)
                return BadRequest(new { message = "Failed retrieve conversation." });

            return Ok(new { messagesResult.Data, page, pageSize, hasMore = messagesResult.Data?.Count == pageSize });
        }

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var messageResult = await _messageService.SendMessageAsync(userIdResult.Value, request);
            if (!messageResult.Success)
                return BadRequest(new { message = "Failed to send message." });

            return Ok(messageResult);
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPut("conversations/{conversationId}/read")]
        public async Task<IActionResult> MarkMessagesAsRead(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            var messeageReadResult = await _messageService.MarkMessagesAsReadAsync(userIdResult.Value, conversationId);
            if (!messeageReadResult.Success)
                return NotFound(new { message = "Failed to mark messages as read" });

            return Ok(new { message = "Messages marked as read" });
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessages(Guid messageId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            var deleteResult = await _messageService.DeleteMessageAsync(userIdResult.Value, messageId);
            if (!deleteResult.Success)
                return NotFound(new { message = "Message not found or permission denied" });

            return Ok(new { message = "Message deleted successfully" });

        }
    }
}
