using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class MessageService : IMessageService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<MessageService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<MessageDTO?> SendMessageAsync(Guid senderId, SendMessageRequest request)
        {
            try
            {
                // Verify conversation exists and user is part of it
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId &&
                        (c.User1Id == senderId || c.User2Id == senderId) && c.IsActive);

                if (conversation == null)
                {
                    return null;
                }

                var message = new Message
                {
                    ConversationId = request.ConversationId,
                    SenderId = senderId,
                    Content = request.Content,
                    MessageType = request.MessageType,
                    IsRead = false,
                    SentAt = DateTime.UtcNow
                };

                _dbContext.Messages.Add(message);

                // Update conversation timestamp
                conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Clear conversation cache
                await _cacheService.RemoveAsync($"conversation_messages_{request.ConversationId}");
                await _cacheService.RemoveAsync($"user_conversations_{senderId}");

                var receiverId = conversation.User1Id == senderId ? conversation.User2Id : conversation.User1Id;
                await _cacheService.RemoveAsync($"user_conversations_{receiverId}");

                // Get sender info for response;
                var sender = await _dbContext.Users.FindAsync(senderId);

                return new MessageDTO
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    SenderId = message.SenderId,
                    SenderName = $"{sender?.FirstName} {sender?.LastName}".Trim(),
                    Content = message.Content,
                    MessageType = message.MessageType,
                    IsRead = message.IsRead,
                    SentAt = message.SentAt
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {SenderId} in conversation {ConversationId}", senderId, request.ConversationId);
                return null;
            }
        }

        public async Task<List<MessageDTO>> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50)
        {
            try
            {
                // Verify user is part of the conversation
                var conversation = await _dbContext.Conversations
                                    .FirstOrDefaultAsync(c => c.Id == conversationId &&
                                                            (c.User1Id == userId || c.User2Id == userId));

                if (conversation == null)
                    return [];

                var cacheKey = $"conversation_messages_{conversationId}_page_{page}";
                var cachedMessages = await _cacheService.GetAsync<List<MessageDTO>>(cacheKey);

                if (cachedMessages != null)
                {
                    return cachedMessages;
                }

                var messages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .Include(m => m.Sender)
                    .OrderByDescending(m => m.SentAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new MessageDTO
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        SenderName = $"{m.Sender.FirstName} {m.Sender.LastName}".Trim(),
                        Content = m.Content,
                        MessageType = m.MessageType,
                        IsRead = m.IsRead,
                        SentAt = m.SentAt
                    })
                    .ToListAsync();

                // Cache for 5 minutes
                await _cacheService.SetAsync(cacheKey, messages, TimeSpan.FromMinutes(5));

                return messages.OrderBy(m => m.SentAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId} for user {UserId}", conversationId, userId);
                return [];
            }
        }

        public async Task<List<ConversationDTO>> GetUserConversationsAsync(Guid userId)
        {
            try
            {
                var cacheKey = $"user_conversations_{userId}";
                var cachedConversations = await _cacheService.GetAsync<List<ConversationDTO>>(cacheKey);

                if (cachedConversations != null)
                    return cachedConversations;

                var conversations = await _dbContext.Conversations
                    .Where(c => (c.User1Id == userId || c.User2Id == userId) && c.IsActive)
                    .Include(c => c.User1)
                        .ThenInclude(u => u.Photos)
                    .Include(c => c.User2)
                        .ThenInclude(u => u.Photos)
                    .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1)) // Get last message
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                var conversationDtos = new List<ConversationDTO>();

                foreach (var conversation in conversations)
                {
                    var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;
                    var unreadCount = await _dbContext.Messages
                        .CountAsync(m => m.ConversationId == conversation.Id && m.SenderId != userId && !m.IsRead);

                    var lastMessage = conversation.Messages.FirstOrDefault();

                    conversationDtos.Add(new ConversationDTO
                    {
                        Id = conversation.Id,
                        OtherUserId = otherUser.Id,
                        OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                        OtherUserPhoto = otherUser.Photos.FirstOrDefault(p => p.IsPrimary)?.Url,
                        LastMessage = lastMessage != null ? new MessageDTO
                        {
                            Id = lastMessage.Id,
                            ConversationId = lastMessage.ConversationId,
                            SenderId = lastMessage.SenderId,
                            SenderName = lastMessage.SenderId == userId ? "You" : otherUser.FirstName ?? "",
                            Content = lastMessage.Content,
                            MessageType = lastMessage.MessageType,
                            IsRead = lastMessage.IsRead,
                            SentAt = lastMessage.SentAt
                        } : null,
                        UnreadCount = unreadCount,
                        UpdatedAt = conversation.UpdatedAt,
                        IsActive = conversation.IsActive
                    });
                }

                // Cache for 10 minutes
                await _cacheService.SetAsync(cacheKey, conversationDtos, TimeSpan.FromMinutes(10));

                return conversationDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> MarkMessagesAsReadAsync(Guid userId, Guid conversationId)
        {
            try
            {
                // Verify user is part of the conversation
                var conversation = await _dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId &&
                (c.User1Id == userId || c.User2Id == userId));

                if (conversation == null)
                {
                    return false;
                }
                ;

                var unreadMessages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                }

                await _dbContext.SaveChangesAsync();

                // Clear cache for this conversation
                await _cacheService.RemoveAsync($"user_conversations_{userId}");
                await _cacheService.RemoveAsync($"conversation_messages_{conversationId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for conversation {ConversationId} for user {UserId}", conversationId, userId);
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid userId, Guid messageId)
        {
            try
            {
                var message = await _dbContext.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

                if (message == null)
                {
                    return false; // Message not found or user is not the sender
                }

                // Soft delete - just update content
                message.Content = "[Message deleted]";
                message.MessageType = "deleted"; // Indicate this is a deleted message

                await _dbContext.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"conversation_messages_{message.ConversationId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                return false;
            }
        }

        public async Task<ConversationDTO?> GetConversationAsync(Guid userId, Guid conversationId)
        {
            try
            {
                var conversation = await _dbContext.Conversations
                    .Where(c => c.Id == conversationId &&
                       (c.User1Id == userId || c.User2Id == userId) && c.IsActive)
                    .Include(c => c.User1)
                        .ThenInclude(u => u.Photos)
                    .FirstOrDefaultAsync();

                if (conversation == null) return null;

                var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;
                var unreadCount = await _dbContext.Messages
                    .CountAsync(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead);

                var lastMessage = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                return new ConversationDTO
                {
                    Id = conversation.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                    OtherUserPhoto = otherUser.Photos.FirstOrDefault(p => p.IsPrimary)?.Url,
                    LastMessage = lastMessage != null ? new MessageDTO
                    {
                        Id = lastMessage.Id,
                        ConversationId = lastMessage.ConversationId,
                        SenderId = lastMessage.SenderId,
                        SenderName = lastMessage.SenderId == userId ? "You" : otherUser.FirstName ?? "",
                        Content = lastMessage.Content,
                        MessageType = lastMessage.MessageType,
                        IsRead = lastMessage.IsRead,
                        SentAt = lastMessage.SentAt
                    } : null,
                    UnreadCount = unreadCount,
                    UpdatedAt = conversation.UpdatedAt,
                    IsActive = conversation.IsActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation {ConversationId} for user {UserId}", conversationId, userId);
                return null;
            }
        }

    }
}
