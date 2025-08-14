using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    public class MessageSendData
    {
        public MessageDTO Message { get; set; } = null!;
        public Guid ReceiverId { get; set; }
        public bool IsFirstMessage { get; set; }
        public int ConversationMessageCount { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class MessageSendResult : OperationResult<MessageSendData>
    {
        private MessageSendResult(bool success, MessageSendData? data, string? errorCode = null, string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static MessageSendResult Successful(MessageDTO message, Guid receiverId, bool isFirstMessage, int conversationMessageCount, Dictionary<string, object>? metadata = null)
        {
            var data = new MessageSendData
            {
                Message = message,
                ReceiverId = receiverId,
                IsFirstMessage = isFirstMessage,
                ConversationMessageCount = conversationMessageCount,
                SentAt = message.SentAt
            };

            return new MessageSendResult(true, data, null, null, metadata);
        }

        public static MessageSendResult ConversationNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Conversation not found or you don't have access to it");

        public static MessageSendResult ConversationBlocked()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "Cannot send message. User may have blocked you or conversation is inactive");

        public static MessageSendResult ValidationError(string message)
            => new(false, null, ErrorCodes.VALIDATION_ERROR, message);

        public static MessageSendResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    // Conversation messages data with pagination
    public class ConversationMessagesData
    {
        public List<MessageDTO> Messages { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public int TotalCount { get; set; }
        public DateTime LastMessageAt { get; set; }
        public bool FromCache { get; set; }
    }

    public class ConversationMessagesResult : OperationResult<ConversationMessagesData>
    {
        private ConversationMessagesResult(bool success, ConversationMessagesData? data, string? errorCode = null,
           string? errorMessage = null, Dictionary<string, object>? metadata = null)
           : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static ConversationMessagesResult Successful(List<MessageDTO> messages, int page, int pageSize,
            bool hasMore, int totalCount, DateTime lastMessageAt, bool fromCache = false,
            Dictionary<string, object>? metadata = null)
        {
            var data = new ConversationMessagesData
            {
                Messages = messages,
                Page = page,
                PageSize = pageSize,
                HasMore = hasMore,
                TotalCount = totalCount,
                LastMessageAt = lastMessageAt,
                FromCache = fromCache
            };

            return new ConversationMessagesResult(true, data, null, null, metadata);
        }

        public static ConversationMessagesResult ConversationNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Conversation not found or you don't have access to it");

        public static ConversationMessagesResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
    /// User conversations list data
    public class UserConversationsData
    {
        public List<ConversationDTO> Conversations { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadConversationsCount { get; set; }
        public int TotalUnreadMessages { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool FromCache { get; set; }
    }

    public class UserConversationsResult : OperationResult<UserConversationsData>
    {
        private UserConversationsResult(bool success, UserConversationsData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static UserConversationsResult Successful(List<ConversationDTO> conversations, int totalCount,
            int unreadConversationsCount, int totalUnreadMessages, bool fromCache = false,
            Dictionary<string, object>? metadata = null)
        {
            var data = new UserConversationsData
            {
                Conversations = conversations,
                TotalCount = totalCount,
                UnreadConversationsCount = unreadConversationsCount,
                TotalUnreadMessages = totalUnreadMessages,
                LastUpdated = DateTime.UtcNow,
                FromCache = fromCache
            };

            return new UserConversationsResult(true, data, null, null, metadata);
        }

        public static UserConversationsResult Error(string message)
        => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Message read status update data
    public class MessagesReadData
    {
        public Guid ConversationId { get; set; }
        public int MessagesMarkedCount { get; set; }
        public DateTime MarkedAt { get; set; }
        public Guid? LastReadMessageId { get; set; }
    }

    public class MessagesReadResult : OperationResult<MessagesReadData>
    {
        private MessagesReadResult(bool success, MessagesReadData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static MessagesReadResult Successful(Guid conversationId, int messagesMarkedCount,
            Guid? lastReadMessageId = null, Dictionary<string, object>? metadata = null)
        {
            var data = new MessagesReadData
            {
                ConversationId = conversationId,
                MessagesMarkedCount = messagesMarkedCount,
                MarkedAt = DateTime.UtcNow,
                LastReadMessageId = lastReadMessageId
            };

            return new MessagesReadResult(true, data, null, null, metadata);
        }

        public static MessagesReadResult ConversationNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Conversation not found or you don't have access to it");

        public static MessagesReadResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Message deletion data
    public class MessageDeletionData
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public bool WasWithinTimeLimit { get; set; }
        public DateTime DeletedAt { get; set; }
        public string DeletionType { get; set; } = "soft"; // soft, hard
    }

    public class MessageDeletionResult : OperationResult<MessageDeletionData>
    {
        private MessageDeletionResult(bool success, MessageDeletionData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static MessageDeletionResult Successful(Guid messageId, Guid conversationId, bool wasWithinTimeLimit,
            string deletionType = "soft", Dictionary<string, object>? metadata = null)
        {
            var data = new MessageDeletionData
            {
                MessageId = messageId,
                ConversationId = conversationId,
                WasWithinTimeLimit = wasWithinTimeLimit,
                DeletedAt = DateTime.UtcNow,
                DeletionType = deletionType
            };

            return new MessageDeletionResult(true, data, null, null, metadata);
        }

        public static MessageDeletionResult MessageNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Message not found or you don't have permission to delete it");

        public static MessageDeletionResult TimeExpired()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "Cannot delete messages older than 1 hour");

        public static MessageDeletionResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    /// Conversation details data
    public class ConversationDetailsData
    {
        public ConversationDTO Conversation { get; set; } = null!;
        public bool IsOtherUserOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public bool CanSendMessages { get; set; }
        public string? RestrictionReason { get; set; }
    }

    public class ConversationDetailsResult : OperationResult<ConversationDetailsData>
    {
        private ConversationDetailsResult(bool success, ConversationDetailsData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static ConversationDetailsResult Successful(ConversationDTO conversation, bool isOtherUserOnline,
            DateTime? lastSeen, bool canSendMessages, string? restrictionReason = null,
            Dictionary<string, object>? metadata = null)
        {
            var data = new ConversationDetailsData
            {
                Conversation = conversation,
                IsOtherUserOnline = isOtherUserOnline,
                LastSeen = lastSeen,
                CanSendMessages = canSendMessages,
                RestrictionReason = restrictionReason
            };

            return new ConversationDetailsResult(true, data, null, null, metadata);
        }

        public static ConversationDetailsResult ConversationNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Conversation not found or you don't have access to it");

        public static ConversationDetailsResult UserInactive()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "The other user is no longer active");

        public static ConversationDetailsResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
}
