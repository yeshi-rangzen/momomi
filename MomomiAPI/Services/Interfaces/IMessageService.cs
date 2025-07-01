using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IMessageService
    {
        Task<MessageDTO?> SendMessageAsync(Guid senderId, SendMessageRequest request);
        Task<List<MessageDTO>> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50);
        Task<List<ConversationDTO>> GetUserConversationsAsync(Guid userId);
        Task<bool> MarkMessagesAsReadAsync(Guid userId, Guid conversationId);
        Task<bool> DeleteMessageAsync(Guid userId, Guid messageId);
        Task<ConversationDTO?> GetConversationAsync(Guid userId, Guid conversationId);
    }
}
