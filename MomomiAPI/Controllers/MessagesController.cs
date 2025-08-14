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
    public class MessagesController : BaseApiController
    {
        private readonly IMessageService _messageService;

        public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
            : base(logger)
        {
            _messageService = messageService;
        }

        /// <summary>
        /// Get user's conversations with comprehensive metadata
        /// </summary>
        [HttpGet("conversations")]
        public async Task<ActionResult<OperationResult<UserConversationsData>>> GetConversations()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetConversations));

            var result = await _messageService.GetUserConversationsAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get specific conversation details with online status
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<ActionResult<OperationResult<ConversationDetailsData>>> GetConversationDetails(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetConversationDetails), new { conversationId });

            var result = await _messageService.GetConversationDetailsAsync(userIdResult.Value, conversationId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get messages in a conversation with pagination
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<ActionResult<OperationResult<ConversationMessagesData>>> GetConversationMessages(
            Guid conversationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetConversationMessages), new { conversationId, page, pageSize });

            var result = await _messageService.GetConversationMessagesAsync(userIdResult.Value, conversationId, page, pageSize);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Send a message in a conversation
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult<OperationResult<MessageSendData>>> SendMessage([FromBody] SendMessageRequest request)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(SendMessage), new
            {
                conversationId = request.ConversationId,
                messageType = request.MessageType,
                contentLength = request.Content?.Length ?? 0
            });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _messageService.SendMessageAsync(userIdResult.Value, request);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Mark messages as read in a conversation
        /// </summary>
        [HttpPut("conversations/{conversationId}/mark-read")]
        public async Task<ActionResult<OperationResult<MessagesReadData>>> MarkMessagesAsRead(Guid conversationId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(MarkMessagesAsRead), new { conversationId });

            var result = await _messageService.MarkMessagesAsReadAsync(userIdResult.Value, conversationId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Delete a message (within time limit)
        /// </summary>
        [HttpDelete("messages/{messageId}")]
        public async Task<ActionResult<OperationResult<MessageDeletionData>>> DeleteMessage(Guid messageId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeleteMessage), new { messageId });

            var result = await _messageService.DeleteMessageAsync(userIdResult.Value, messageId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Check if a user is currently online
        /// </summary>
        [HttpGet("users/{userId}/online-status")]
        public async Task<ActionResult<bool>> CheckUserOnlineStatus(Guid userId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(CheckUserOnlineStatus), new { checkedUserId = userId });

            var isOnline = await _messageService.IsUserOnlineAsync(userId);
            return Ok(isOnline);
        }

        /// <summary>
        /// Get conversation statistics for the current user
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<OperationResult<UserConversationsData>>> GetConversationStatistics()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetConversationStatistics));

            // Return the same data as conversations but client can use metadata
            var result = await _messageService.GetUserConversationsAsync(userIdResult.Value);
            return HandleOperationResult(result);
        }
    }

    /// <summary>
    /// Request models for messaging endpoints
    /// </summary>
    public class MarkMessagesReadRequest
    {
        public Guid ConversationId { get; set; }
    }

    public class GetMessagesRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}