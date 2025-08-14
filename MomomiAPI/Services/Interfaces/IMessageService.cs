using MomomiAPI.Common.Results;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMessageService
    {
        /// <summary>
        /// Sends a message in a conversation with optimized operations
        /// </summary>
        Task<MessageSendResult> SendMessageAsync(Guid senderId, SendMessageRequest request);

        /// <summary>
        /// Gets conversation messages with pagination and caching
        /// </summary>
        Task<ConversationMessagesResult> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50);

        /// <summary>
        /// Gets user conversations with comprehensive metadata
        /// </summary>
        Task<UserConversationsResult> GetUserConversationsAsync(Guid userId);

        /// <summary>
        /// Marks messages as read with batch operations
        /// </summary>
        Task<MessagesReadResult> MarkMessagesAsReadAsync(Guid userId, Guid conversationId);

        /// <summary>
        /// Deletes a message with time restrictions
        /// </summary>
        Task<MessageDeletionResult> DeleteMessageAsync(Guid userId, Guid messageId);

        /// <summary>
        /// Gets conversation details with online status
        /// </summary>
        Task<ConversationDetailsResult> GetConversationDetailsAsync(Guid userId, Guid conversationId);

        /// <summary>
        /// Checks if a user is currently online
        /// </summary>
        Task<bool> IsUserOnlineAsync(Guid userId);
    }
}