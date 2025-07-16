using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
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
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            ILogger<MessageService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
        }

        public async Task<OperationResult<MessageDTO>> SendMessageAsync(Guid senderId, SendMessageRequest request)
        {
            try
            {
                _logger.LogInformation("User {SenderId} sending message to conversation {ConversationId}", senderId, request.ConversationId);

                // Validate message content
                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    return OperationResult<MessageDTO>.ValidationFailure("Message content cannot be empty.");
                }

                if (request.Content.Length > 1000)
                {
                    return OperationResult<MessageDTO>.ValidationFailure("Message cannot exceed 1000 characters.");
                }

                // Verify conversation exists and user is part of it
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId &&
                        (c.User1Id == senderId || c.User2Id == senderId) && c.IsActive);

                if (conversation == null)
                {
                    return OperationResult<MessageDTO>.NotFound("Conversation not found or you don't have access to it.");
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
                await _cacheInvalidation.InvalidateConversationCache(request.ConversationId);
                await _cacheInvalidation.InvalidateUserConversations(senderId);

                var receiverId = conversation.User1Id == senderId ? conversation.User2Id : conversation.User1Id;
                await _cacheInvalidation.InvalidateUserConversations(receiverId);

                // Get sender info for response;
                var sender = await _dbContext.Users.FindAsync(senderId);

                var messageDto = new MessageDTO
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

                _logger.LogInformation("Message {MessageId} sent successfully from user {SenderId}", message.Id, senderId);
                return OperationResult<MessageDTO>.Successful(messageDto)
                    .WithMetadata("conversation_id", request.ConversationId)
                    .WithMetadata("receiver_id", receiverId);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {SenderId} in conversation {ConversationId}", senderId, request.ConversationId);
                return OperationResult<MessageDTO>.Failed("Unable to send message. Please try again.");
            }
        }

        public async Task<OperationResult<List<MessageDTO>>> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogDebug("Retrieving messages for conversation {ConversationId}, page {Page}", conversationId, page);

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                // Verify user is part of the conversation
                var conversation = await _dbContext.Conversations
                                    .FirstOrDefaultAsync(c => c.Id == conversationId &&
                                                            (c.User1Id == userId || c.User2Id == userId));

                if (conversation == null)
                {
                    return OperationResult<List<MessageDTO>>.NotFound("Conversation not found or you don't have access to it.");
                }

                var cacheKey = CacheKeys.Messaging.ConversationMessages(conversationId, page);
                var cachedMessages = await _cacheService.GetAsync<List<MessageDTO>>(cacheKey);

                if (cachedMessages != null)
                {
                    _logger.LogDebug("Returning cached messages for conversation {ConversationId}, page {Page}", conversationId, page);
                    return OperationResult<List<MessageDTO>>.Successful(cachedMessages);
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

                // Order by sent time for display (newest first becomes oldest first for chat display)
                var orderedMessages = messages.OrderBy(m => m.SentAt).ToList();

                // Cache for 5 minutes
                await _cacheService.SetAsync(cacheKey, orderedMessages, CacheKeys.Duration.Conversations);

                _logger.LogDebug("Retrieved {Count} messages for conversation {ConversationId}, page {Page}",
                    orderedMessages.Count, conversationId, page);

                return OperationResult<List<MessageDTO>>.Successful(orderedMessages)
                    .WithMetadata("page", page)
                    .WithMetadata("page_size", pageSize)
                    .WithMetadata("has_more", messages.Count == pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId} for user {UserId}", conversationId, userId);
                return OperationResult<List<MessageDTO>>.Failed("Unable to retrieve messages. Please try again.");
            }
        }

        public async Task<OperationResult<List<ConversationDTO>>> GetUserConversationsAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving conversations for user {UserId}", userId);

                var cacheKey = CacheKeys.Messaging.UserConversations(userId);
                var cachedConversations = await _cacheService.GetAsync<List<ConversationDTO>>(cacheKey);

                if (cachedConversations != null)
                {
                    _logger.LogDebug("Returning cached conversations for user {UserId}", userId);
                    return OperationResult<List<ConversationDTO>>.Successful(cachedConversations);
                }

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

                    // Skip if other user is inactive
                    if (!otherUser.IsActive)
                        continue;

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
                await _cacheService.SetAsync(cacheKey, conversationDtos, CacheKeys.Duration.Conversations);

                _logger.LogDebug("Retrieved {Count} conversations for user {UserId}", conversationDtos.Count, userId);
                return OperationResult<List<ConversationDTO>>.Successful(conversationDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
                return OperationResult<List<ConversationDTO>>.Failed("Unable to retrieve conversations. Please try again.");
            }
        }

        public async Task<OperationResult> MarkMessagesAsReadAsync(Guid userId, Guid conversationId)
        {
            try
            {
                _logger.LogDebug("Marking messages as read for user {UserId} in conversation {ConversationId}", userId, conversationId);

                // Verify user is part of the conversation
                var conversation = await _dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId &&
                (c.User1Id == userId || c.User2Id == userId));

                if (conversation == null)
                {
                    return OperationResult.NotFound("Conversation not found or you don't have access to it.");
                }

                var unreadMessages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                    .ToListAsync();

                if (!unreadMessages.Any())
                {
                    return OperationResult.Successful().WithMetadata("messages_marked", 0);
                }

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                }

                await _dbContext.SaveChangesAsync();

                // Clear relevant caches
                await _cacheInvalidation.InvalidateUserConversations(userId);
                await _cacheInvalidation.InvalidateConversationCache(conversationId);

                _logger.LogDebug("Marked {Count} messages as read for user {UserId}", unreadMessages.Count, userId);
                return OperationResult.Successful()
                    .WithMetadata("messages_marked", unreadMessages.Count)
                    .WithMetadata("marked_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for conversation {ConversationId} for user {UserId}", conversationId, userId);
                return OperationResult.Failed("Unable to mark messages as read. Please try again.");
            }
        }

        public async Task<OperationResult> DeleteMessageAsync(Guid userId, Guid messageId)
        {
            try
            {
                _logger.LogInformation("User {UserId} attempting to delete message {MessageId}", userId, messageId);

                var message = await _dbContext.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

                if (message == null)
                {
                    return OperationResult.NotFound("Message not found or you don't have permission to delete it.");
                }

                // Check if message is too old to delete (e.g., older than 1 hour)
                if (DateTime.UtcNow - message.SentAt > TimeSpan.FromHours(1))
                {
                    return OperationResult.BusinessRuleViolation("Cannot delete messages older than 1 hour.");
                }

                // Soft delete - just update content
                message.Content = "[Message deleted]";
                message.MessageType = "deleted"; // Indicate this is a deleted message

                await _dbContext.SaveChangesAsync();

                // Clear cache
                await _cacheInvalidation.InvalidateConversationCache(message.ConversationId);

                _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);
                return OperationResult.Successful()
                    .WithMetadata("deleted_at", DateTime.UtcNow)
                    .WithMetadata("conversation_id", message.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                return OperationResult.Failed("Unable to delete message. Please try again.");
            }
        }

        public async Task<OperationResult<ConversationDTO>> GetConversationAsync(Guid userId, Guid conversationId)
        {
            try
            {
                _logger.LogDebug("Retrieving conversation {ConversationId} for user {UserId}", conversationId, userId);

                var conversation = await _dbContext.Conversations
                    .Where(c => c.Id == conversationId &&
                       (c.User1Id == userId || c.User2Id == userId) && c.IsActive)
                    .Include(c => c.User1)
                        .ThenInclude(u => u.Photos)
                    .Include(c => c.User2)
                        .ThenInclude(u => u.Photos)
                    .FirstOrDefaultAsync();

                if (conversation == null)
                {
                    return OperationResult<ConversationDTO>.NotFound("Conversation not found or you don't have access to it.");
                }

                var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;

                if (!otherUser.IsActive)
                {
                    return OperationResult<ConversationDTO>.NotFound("The other user is no longer active.");
                }

                var unreadCount = await _dbContext.Messages
                    .CountAsync(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead);

                var lastMessage = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var conversationDto = new ConversationDTO
                {
                    Id = conversation.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                    OtherUserPhoto = otherUser.Photos.FirstOrDefault(p => p.IsPrimary)?.Url ??
                                   otherUser.Photos.OrderBy(p => p.PhotoOrder).FirstOrDefault()?.Url,
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

                return OperationResult<ConversationDTO>.Successful(conversationDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation {ConversationId} for user {UserId}", conversationId, userId);
                return OperationResult<ConversationDTO>.Failed("Unable to retrieve conversation. Please try again.");
            }
        }

    }
}
