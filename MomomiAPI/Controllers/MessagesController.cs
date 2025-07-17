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
        public async Task<ActionResult<List<ConversationDTO>>> GetConversations()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversations), new { userIdResult.Value });

            var result = await _messageService.GetUserConversationsAsync(userIdResult.Value);
            return HandleOperationResult(result);

        }

        /// <summary>
        /// Get specific conversation details
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<ActionResult<ConversationDTO>> GetConversation(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversation), new { userIdResult.Value });

            var result = await _messageService.GetConversationAsync(userIdResult.Value, conversationId);
            return HandleOperationResult(result);

        }

        /// <summary>
        /// Get messages in a conversation
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<ActionResult<List<MessageDTO>>> GetConversationMessages(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(GetConversationMessages), new { userIdResult.Value });

            var result = await _messageService.GetConversationMessagesAsync(userIdResult.Value, conversationId, page, pageSize);

            if (!result.Success)
            {
                return HandleOperationResult(result);
            }

            return Ok(new
            {
                data = result.Data,
                page,
                pageSize,
                hasMore = result.Data?.Count == pageSize,
                metadata = result.Metadata
            });
        }

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult<MessageDTO>> SendMessage([FromBody] SendMessageRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _messageService.SendMessageAsync(userIdResult.Value, request);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPut("conversations/{conversationId}/read")]
        public async Task<ActionResult> MarkMessagesAsRead(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            var result = await _messageService.MarkMessagesAsReadAsync(userIdResult.Value, conversationId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpDelete("{messageId}")]
        public async Task<ActionResult> DeleteMessages(Guid messageId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null)
                return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new { userIdResult.Value });

            var result = await _messageService.DeleteMessageAsync(userIdResult.Value, messageId);
            return HandleOperationResult(result);
        }
    }
}
