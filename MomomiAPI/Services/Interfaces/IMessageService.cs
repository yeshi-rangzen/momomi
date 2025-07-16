using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMessageService
    {
        Task<OperationResult<MessageDTO>> SendMessageAsync(Guid senderId, SendMessageRequest request);
        Task<OperationResult<List<MessageDTO>>> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50);
        Task<OperationResult<List<ConversationDTO>>> GetUserConversationsAsync(Guid userId);
        Task<OperationResult> MarkMessagesAsReadAsync(Guid userId, Guid conversationId);
        Task<OperationResult> DeleteMessageAsync(Guid userId, Guid messageId);
        Task<OperationResult<ConversationDTO>> GetConversationAsync(Guid userId, Guid conversationId);
    }
}
