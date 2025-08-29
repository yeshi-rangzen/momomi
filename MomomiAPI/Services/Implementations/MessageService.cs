using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
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

        // Sends a message in a conversation with optimized operations
        public async Task<MessageSendResult> SendMessageAsync(Guid senderId, SendMessageRequest request)
        {
            try
            {
                _logger.LogInformation("User {SenderId} is sending a message to conversation {ConversationId}", senderId, request.ConversationId);

                // Validate message content
                var validationResult = ValidateMessageContent(request.Content, request.MessageType);
                if (!validationResult.Success)
                {
                    return MessageSendResult.ValidationError(validationResult.ErrorMessage!);
                }

                // Get conversation data and validate access in a single query
                var conversationData = await GetConversationDataForSending(senderId, request.ConversationId);
                if (!conversationData.IsValid)
                {
                    return conversationData.ErrorResult!;
                }

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {

                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Create message
                        var message = new Message
                        {
                            SenderId = senderId,
                            ConversationId = request.ConversationId,
                            Content = request.Content,
                            MessageType = request.MessageType,
                            IsRead = false,
                            SentAt = DateTime.UtcNow
                        };

                        _dbContext.Messages.Add(message);

                        // Update conversation timestamp
                        conversationData.Conversation!.UpdatedAt = DateTime.UtcNow;

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Get final message count
                        var messageCount = await GetConversationMessageCount(request.ConversationId);

                        // Create DTO for response
                        var messageDto = CreateMessageDTO(message, conversationData.SenderName!, senderId);

                        // Invalidate relevant caches (fire and forget for performance)
                        _ = Task.Run(async () => await InvalidateMessagingCaches(request.ConversationId, senderId, conversationData.ReceiverId!.Value));

                        _logger.LogInformation("Message {MessageId} sent successfully from user {SenderId}", message.Id, senderId);

                        return MessageSendResult.Successful(messageDto, conversationData.ReceiverId!.Value, conversationData.IsFirstMessage, messageCount);
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from user {SenderId} in conversation {ConversationId}",
                    senderId, request.ConversationId);
                return MessageSendResult.Error("Unable to send message. Please try again.");
            }
        }

        // Gets conversation messages with optimized caching and pagination
        public async Task<ConversationMessagesResult> GetConversationMessagesAsync(Guid userId, Guid conversationId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogDebug("Retrieving messages for conversation {ConversationId}, page {Page}", conversationId, page);

                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                // Very user access to conversation
                var hasAccess = await VerifyConversationAccess(userId, conversationId);
                if (!hasAccess)
                {
                    return ConversationMessagesResult.ConversationNotFound();
                }

                var cacheKey = CacheKeys.Messaging.ConversationMessages(conversationId, page);
                var messagesData = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await FetchConversationMessagesFromDatabase(userId, conversationId, page, pageSize),
                    CacheKeys.Duration.Conversations);

                if (messagesData == null)
                {
                    return ConversationMessagesResult.Error("Failed to retrieve messages");
                }

                _logger.LogDebug("Retrieved {Count} messages for conversation {ConversationId}, page {Page}",
                                   messagesData.Messages.Count, conversationId, page);

                return ConversationMessagesResult.Successful(
                    messagesData.Messages,
                    messagesData.Page,
                    messagesData.PageSize,
                    messagesData.HasMore,
                    messagesData.TotalCount,
                    messagesData.LastMessageAt,
                    fromCache: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for conversation {ConversationId} for user {UserId}",
                    conversationId, userId);
                return ConversationMessagesResult.Error("Unable to retrieve messages. Please try again.");
            }
        }

        // Gets user conversations with optimized caching and metadata
        public async Task<UserConversationsResult> GetUserConversationsAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving conversations for user {UserId}", userId);
                var cacheKey = CacheKeys.Messaging.UserConversations(userId);

                var conversationsData = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await FetchUserConversationsFromDatabase(userId),
                    CacheKeys.Duration.Conversations);

                if (conversationsData == null)
                {
                    conversationsData = new UserConversationsData { Conversations = [] };
                }

                return UserConversationsResult.Successful(
                    conversationsData.Conversations,
                    conversationsData.TotalCount,
                    conversationsData.UnreadConversationsCount,
                    conversationsData.TotalUnreadMessages,
                    fromCache: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations for user {UserId}", userId);
                return UserConversationsResult.Error("Unable to retrieve conversations. Please try again.");
            }
        }

        // Mark messages as read with optimized batch operations
        public async Task<MessagesReadResult> MarkMessagesAsReadAsync(Guid userId, Guid conversationId)
        {
            try
            {
                _logger.LogDebug("Marking messages as read for user {UserId} in conversation {ConversationId}", userId, conversationId);

                // Verify user has access to conversation
                var hasAccess = await VerifyConversationAccess(userId, conversationId);
                if (!hasAccess)
                {
                    return MessagesReadResult.ConversationNotFound();
                }

                // Get unread messages for this user
                var unreadMessages = await _dbContext.Messages
                    .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                    .ToListAsync();

                if (!unreadMessages.Any())
                {
                    return MessagesReadResult.Successful(conversationId, []);
                }

                // Mark all as read
                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                // Get the last read message ID
                var lastReadMessageId = unreadMessages.OrderByDescending(m => m.SentAt).FirstOrDefault()?.Id ?? Guid.Empty;

                // Invalidate relevant caches
                await InvalidateReadStatusCaches(userId, conversationId);
                _logger.LogDebug("Marked {Count} messages as read for user {UserId}", unreadMessages.Count, userId);

                return MessagesReadResult.Successful(conversationId, unreadMessages.Select(msg => msg.Id).ToList(), lastReadMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for conversation {ConversationId} for user {UserId}",
                    conversationId, userId);
                return MessagesReadResult.Error("Unable to mark messages as read. Please try again.");
            }
        }

        // Deletes a message with time-based restrictions
        public async Task<MessageDeletionResult> DeleteMessageAsync(Guid userId, Guid messageId)
        {
            try
            {
                _logger.LogInformation("User {UserId} attempting to delete message {MessageId}", userId, messageId);

                var message = await _dbContext.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

                if (message == null)
                {
                    return MessageDeletionResult.MessageNotFound();
                }

                // Check if message is within deletion time limit
                var isWithinTimeLimit = DateTime.UtcNow - message.SentAt <= AppConstants.Limits.MessageDeletionTimeLimit;
                if (!isWithinTimeLimit)
                {
                    return MessageDeletionResult.TimeExpired();
                }

                // Soft delete - update content to indicate deletion
                message.Content = "[Message deleted]";
                message.MessageType = "deleted";
                message.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Invalidate conversation caches
                await InvalidateConversationCaches(message.ConversationId);

                _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);

                return MessageDeletionResult.Successful(messageId, message.ConversationId, isWithinTimeLimit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                return MessageDeletionResult.Error("Unable to delete message. Please try again.");
            }
        }

        // Gets conversatoin details with online status and restrictions
        public async Task<ConversationDetailsResult> GetConversationDetailsAsync(Guid userId, Guid conversationId)
        {
            try
            {
                _logger.LogDebug("Retrieving conversation {ConversationId} for user {UserId}", conversationId, userId);

                var conversation = await _dbContext.Conversations
                    .Where(c => c.Id == conversationId &&
                               (c.User1Id == userId || c.User2Id == userId) &&
                               c.IsActive)
                    .Include(c => c.User1)
                        .ThenInclude(u => u.Photos)
                    .Include(c => c.User2)
                        .ThenInclude(u => u.Photos)
                    .FirstOrDefaultAsync();

                if (conversation == null)
                {
                    return ConversationDetailsResult.ConversationNotFound();
                }

                var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;

                if (!otherUser.IsActive)
                {
                    return ConversationDetailsResult.UserInactive();
                }

                // Check if other user is online
                var isOtherUserOnline = await IsUserOnlineAsync(otherUser.Id);

                // Get conversation summary data
                var summaryData = await GetConversationSummaryData(conversationId, userId);

                var conversationDto = new ConversationDTO
                {
                    Id = conversation.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                    OtherUserPhoto = otherUser.Photos.FirstOrDefault(p => p.IsPrimary)?.Url ??
                                   otherUser.Photos.OrderBy(p => p.PhotoOrder).FirstOrDefault()?.Url,
                    LastMessage = summaryData.LastMessage,
                    UnreadCount = summaryData.UnreadCount,
                    UpdatedAt = conversation.UpdatedAt,
                    IsActive = conversation.IsActive
                };

                return ConversationDetailsResult.Successful(
                    conversationDto,
                    isOtherUserOnline,
                    lastSeen: null, // Could implement last seen tracking
                    canSendMessages: true // Could implement blocking/restriction checks
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation {ConversationId} for user {UserId}",
                    conversationId, userId);
                return ConversationDetailsResult.Error("Unable to retrieve conversation. Please try again.");
            }
        }

        /// Checks if a user is currently online (cached check)
        public async Task<bool> IsUserOnlineAsync(Guid userId)
        {
            try
            {
                return await _cacheService.ExistsAsync(CacheKeys.Messaging.UserOnline(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking online status for user {UserId}", userId);
                return false; // Assume offline on error
            }
        }

        public async Task SetUserOnlineStatus(Guid userId, bool isOnline)
        {
            try
            {
                var cacheKey = CacheKeys.Messaging.UserOnline(userId);

                if (isOnline)
                {
                    await _cacheService.SetStringAsync(cacheKey, "true", CacheKeys.Duration.UserOnlineStatus);
                }
                else
                {
                    await _cacheService.RemoveAsync(cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update online status for user {UserId}", userId);
            }
        }


        #region Private Helper Methods
        // Validates message content and type
        private static OperationResult ValidateMessageContent(string content, string messageType)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return OperationResult.ValidationFailure("Message content cannot be empty");
            }

            if (content.Length > 1000)
            {
                return OperationResult.ValidationFailure("Message cannot exceed 1000 characters");
            }

            var allowedMessageTypes = new[] { "text", "image", "emoji", "deleted" };
            if (!allowedMessageTypes.Contains(messageType))
            {
                return OperationResult.ValidationFailure("Invalid message type");
            }

            return OperationResult.Successful();
        }

        // Gets conversation data needed for sending messages in a single optimized query
        private async Task<ConversationSendData> GetConversationDataForSending(Guid senderId, Guid conversationId)
        {
            var conversationQuery = await _dbContext.Conversations
                .Where(c => c.Id == conversationId &&
                    (c.User1Id == senderId || c.User2Id == senderId) &&
                    c.IsActive)
                .Select(c => new
                {
                    Conversation = c,
                    ReceiverId = c.User1Id == senderId ? c.User2Id : c.User1Id,
                    SenderUserName = c.User1Id == senderId ? c.User1.FirstName : c.User2.FirstName,
                    IsReceiverActive = c.User1Id == senderId ? c.User2.IsActive : c.User1.IsActive,
                    IsFirstMessage = c.Messages.Count() == 0,
                })
                .FirstOrDefaultAsync();

            if (conversationQuery?.Conversation == null)
            {
                return ConversationSendData.Invalid(MessageSendResult.ConversationNotFound());
            }

            if (!conversationQuery.IsReceiverActive)
            {
                return ConversationSendData.Invalid(MessageSendResult.ConversationBlocked());
            }

            return ConversationSendData.Valid(
                conversationQuery.Conversation, conversationQuery.ReceiverId, conversationQuery.SenderUserName ?? "User", conversationQuery.IsFirstMessage);
        }

        // Verifies if user has access to conversation
        private async Task<bool> VerifyConversationAccess(Guid userId, Guid conversationId)
        {
            return await _dbContext.Conversations
                .AnyAsync(c => c.Id == conversationId &&
                            (c.User1Id == userId || c.User2Id == userId) &&
                            c.IsActive);
        }

        // Fetches conversation message from database with pagination
        private async Task<ConversationMessagesData> FetchConversationMessagesFromDatabase(
            Guid userId, Guid conversationId, int page, int pageSize)
        {
            var totalCount = await _dbContext.Messages.CountAsync(m => m.ConversationId == conversationId);

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
                    SenderName = m.Sender.FirstName ?? "",
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    SentAt = m.SentAt
                })
                .ToListAsync();

            // Order by sent time for display (newest first becomes oldest first for chat display)
            var orderedMessages = messages.OrderBy(m => m.SentAt).ToList();

            return new ConversationMessagesData
            {
                Messages = orderedMessages,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount,
                TotalCount = totalCount,
                LastMessageAt = orderedMessages.LastOrDefault()?.SentAt ?? DateTime.MinValue,
                FromCache = false
            };
        }

        // Fetches user conversations from database with all metadata
        private async Task<UserConversationsData> FetchUserConversationsFromDatabase(Guid userId)
        {
            // First, get conversations with proper includes
            var conversations = await _dbContext.Conversations
                .Where(c => (c.User1Id == userId || c.User2Id == userId) && c.IsActive)
                .Include(c => c.User1)
                    .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .Include(c => c.User2)
                    .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .Where(c => c.User1Id == userId ? c.User2.IsActive : c.User1.IsActive)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            // Get unread counts for all conversations in a single query
            var conversationIds = conversations.Select(c => c.Id).ToList();
            var unreadCounts = await _dbContext.Messages
                .Where(m => conversationIds.Contains(m.ConversationId) &&
                           m.SenderId != userId &&
                           !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

            // Transform to DTOs in memory
            var conversationDtos = conversations.Select(conversation =>
            {
                var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;
                var lastMessage = conversation.Messages.FirstOrDefault();

                return new ConversationDTO
                {
                    Id = conversation.Id,
                    OtherUserId = otherUser.Id,
                    OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                    OtherUserPhoto = otherUser.Photos.FirstOrDefault()?.Url,
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
                    UnreadCount = unreadCounts.GetValueOrDefault(conversation.Id, 0),
                    UpdatedAt = conversation.UpdatedAt,
                    IsActive = conversation.IsActive
                };
            }).ToList();

            return new UserConversationsData
            {
                Conversations = conversationDtos,
                TotalCount = conversationDtos.Count,
                UnreadConversationsCount = conversationDtos.Count(c => c.UnreadCount > 0),
                TotalUnreadMessages = conversationDtos.Sum(c => c.UnreadCount),
                LastUpdated = DateTime.UtcNow,
                FromCache = false
            };
        }

        // Gets conversation summary data (last message and unread count)
        private async Task<ConversationSummaryData> GetConversationSummaryData(Guid conversationId, Guid userId)
        {
            var summaryQuery = await _dbContext.Messages
               .Where(m => m.ConversationId == conversationId)
               .GroupBy(m => m.ConversationId)
               .Select(g => new
               {
                   LastMessage = g.OrderByDescending(m => m.SentAt).FirstOrDefault(),
                   UnreadCount = g.Count(m => m.SenderId != userId && !m.IsRead)
               })
               .FirstOrDefaultAsync();

            MessageDTO? lastMessageDto = null;
            if (summaryQuery?.LastMessage != null)
            {
                var sender = await _dbContext.Users.FindAsync(summaryQuery.LastMessage.SenderId);
                lastMessageDto = new MessageDTO
                {
                    Id = summaryQuery.LastMessage.Id,
                    ConversationId = summaryQuery.LastMessage.ConversationId,
                    SenderId = summaryQuery.LastMessage.SenderId,
                    SenderName = summaryQuery.LastMessage.SenderId == userId ? "You" : sender?.FirstName ?? "",
                    Content = summaryQuery.LastMessage.Content,
                    MessageType = summaryQuery.LastMessage.MessageType,
                    IsRead = summaryQuery.LastMessage.IsRead,
                    SentAt = summaryQuery.LastMessage.SentAt
                };
            }

            return new ConversationSummaryData
            {
                LastMessage = lastMessageDto,
                UnreadCount = summaryQuery?.UnreadCount ?? 0
            };
        }

        /// Gets conversation message count (cached)
        private async Task<int> GetConversationMessageCount(Guid conversationId)
        {
            var cacheKey = CacheKeys.Messaging.ConversationCount(conversationId);

            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () => await _dbContext.Messages.CountAsync(m => m.ConversationId == conversationId),
                TimeSpan.FromMinutes(5)
            );
        }

        /// Creates MessageDTO from Message entity
        private static MessageDTO CreateMessageDTO(Message message, string senderName, Guid currentUserId)
        {
            return new MessageDTO
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = message.SenderId == currentUserId ? senderName : "Unidentified Sender",
                Content = message.Content,
                MessageType = message.MessageType,
                IsRead = message.IsRead,
                SentAt = message.SentAt
            };
        }

        /// Invalidates messaging-related caches after sending a message
        /// Used when a message is sent 
        private async Task InvalidateMessagingCaches(Guid conversationId, Guid senderId, Guid receiverId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    CacheKeys.Messaging.UserConversations(senderId),
                    CacheKeys.Messaging.UserConversations(receiverId),
                    CacheKeys.Messaging.ConversationDetails(conversationId),
                    CacheKeys.Messaging.ConversationCount(conversationId),
                };

                // Invalidate conversation message cache pages (first few pages likely to change)
                for (int page = 1; page <= 3; page++)
                {
                    keysToInvalidate.Add(CacheKeys.Messaging.ConversationMessages(conversationId, page));
                }

                await _cacheService.RemoveManyAsync(keysToInvalidate);
                _logger.LogDebug("Invalidated {Count} messaging cache keys for conversation {ConversationId}",
                    keysToInvalidate.Count, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate messaging caches for conversation {ConversationId}",
                    conversationId);
            }
        }

        /// Invalidates read status related caches
        /// Used when messages are marked as read
        private async Task InvalidateReadStatusCaches(Guid userId, Guid conversationId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    CacheKeys.Messaging.UserConversations(userId),
                    CacheKeys.Messaging.ConversationDetails(conversationId)
                };

                // Invalidate message cache pages as read status changed
                for (int page = 1; page <= 5; page++)
                {
                    keysToInvalidate.Add(CacheKeys.Messaging.ConversationMessages(conversationId, page));
                }

                await _cacheService.RemoveManyAsync(keysToInvalidate);
                _logger.LogDebug("Invalidated read status caches for user {UserId} in conversation {ConversationId}",
                    userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate read status caches for user {UserId}", userId);
            }
        }

        /// Invalidates conversation-specific caches
        /// Used when message is deleted 
        private async Task InvalidateConversationCaches(Guid conversationId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    CacheKeys.Messaging.ConversationDetails(conversationId),
                    CacheKeys.Messaging.ConversationCount(conversationId),
                };

                // Invalidate all message pages for this conversation
                for (int page = 1; page <= 10; page++)
                {
                    keysToInvalidate.Add(CacheKeys.Messaging.ConversationMessages(conversationId, page));
                }

                await _cacheService.RemoveManyAsync(keysToInvalidate);
                _logger.LogDebug("Invalidated conversation caches for {ConversationId}", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate conversation caches for {ConversationId}", conversationId);
            }
        }
        #endregion

        #region Helper Classes

        // Helper class for conversation sending data validation
        private class ConversationSendData
        {
            public bool IsValid { get; set; }
            public Conversation? Conversation { get; set; }
            public Guid? ReceiverId { get; set; }
            public string? SenderName { get; set; }
            public bool IsFirstMessage { get; set; }
            public MessageSendResult? ErrorResult { get; set; }

            public static ConversationSendData Valid(Conversation conversation, Guid receiverId,
                string senderName, bool isFirstMessage) => new()
                {
                    IsValid = true,
                    Conversation = conversation,
                    ReceiverId = receiverId,
                    SenderName = senderName,
                    IsFirstMessage = isFirstMessage
                };

            public static ConversationSendData Invalid(MessageSendResult errorResult) => new()
            {
                IsValid = false,
                ErrorResult = errorResult
            };
        }

        // Helper class for conversation summary data
        private class ConversationSummaryData
        {
            public MessageDTO? LastMessage { get; set; }
            public int UnreadCount { get; set; }
        }
        #endregion
    }
}
