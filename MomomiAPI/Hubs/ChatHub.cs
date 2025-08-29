using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IMessageService messageService, ICacheService cacheService, ILogger<ChatHub> logger)
        {
            _messageService = messageService;
            _cacheService = cacheService;
            _logger = logger;
        }

        #region Connection Management

        /// <summary>
        /// User joins a specific conversation room
        /// </summary>
        public async Task<List<string>> JoinConversationRoom(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return []; // Return empty list on error
                }

                // Verify user has access to this conversation
                var conversationResult = await _messageService.GetConversationDetailsAsync(userId.Value, Guid.Parse(conversationId));
                if (!conversationResult.Success)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied to conversation");
                    return []; // Return empty list on error
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));

                // Track user joining the conversation
                await TrackUserInConversation(conversationId, userId.Value, Context.ConnectionId, true);
                await TrackUserConversations(userId.Value, conversationId, true);

                // Update user's online status
                await _messageService.SetUserOnlineStatus(userId.Value, true);

                // Get all users currently in the conversation (including the one just joined)
                var connectedUsers = await GetConnectedUsersInConversation(conversationId);

                // Get other users (excluding the current user who just joined)
                var otherUsers = connectedUsers
                    .Where(u => u.UserId != userId.ToString())
                    .Select(u => u.UserId) // Just return user IDs as strings
                    .ToList();

                // Notify other users in conversation that this user is online
                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("UserJoinedConversation", userId.Value);

                _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);

                return otherUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining conversation {ConversationId}", conversationId);
                await Clients.Caller.SendAsync("Error", "Failed to join conversation");
                return []; // Return empty list on error
            }
        }

        /// <summary>
        /// User leaves a specific conversation room
        /// </summary>
        public async Task LeaveConversationRoom(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                // Log users before leaving
                await LogUsersInConversation(conversationId, "LeaveConversationRoom", userId.Value);

                // Track user leaving the conversation
                await TrackUserInConversation(conversationId, userId.Value, Context.ConnectionId, false);
                await TrackUserConversations(userId.Value, conversationId, false);

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));

                // Notify others that user left
                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("UserLeftConversation", userId.Value);

                _logger.LogInformation("User {UserId} left conversation {ConversationId}", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving conversation {ConversationId}", conversationId);
            }
        }

        #endregion

        #region Message Operations

        /// <summary>
        /// Send a real-time message through SignalR
        /// </summary>
        public async Task SendRealtimeMessage(string conversationId, string content, string messageType = "text")
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Log all users in conversation before sending message
                await LogUsersInConversation(conversationId, "SendRealtimeMessage", userId.Value);

                var request = new SendMessageRequest
                {
                    ConversationId = Guid.Parse(conversationId),
                    Content = content,
                    MessageType = messageType
                };

                // Send message using the service
                var result = await _messageService.SendMessageAsync(userId.Value, request);
                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("MessageSendError", result.ErrorMessage);
                    return;
                }

                var messageData = result.Data!;

                // Log who will receive the message
                var connectedUsers = await GetConnectedUsersInConversation(conversationId);
                _logger.LogInformation("Sending message {MessageId} to {RecipientCount} connected users in conversation {ConversationId}: [{Recipients}]",
                    messageData.Message.Id,
                    connectedUsers.Count,
                    conversationId,
                    string.Join(", ", connectedUsers.Select(u => u.UserId)));

                // Send to all users in the conversation room
                await Clients.Group(GetConversationGroupName(conversationId))
                    .SendAsync("MessageReceived", messageData.Message);
                _logger.LogDebug("Real-time message {MessageId} sent in conversation {ConversationId}",
                messageData.Message.Id, conversationId);


                var isReceiverInConversation = connectedUsers.Any(u => u.UserId == messageData.ReceiverId.ToString());
                var isReceiverOnline = await _messageService.IsUserOnlineAsync(messageData.ReceiverId);

                if (!isReceiverInConversation && isReceiverOnline)
                {

                    // Send push notification to online users but not in conversation
                    await SendOfflineNotification(conversationId, messageData.ReceiverId, messageData.Message);

                    _logger.LogDebug("Real-time message sent in to User because they are online");

                }

                _logger.LogDebug("Real-time message Should be sent via Push Notifications");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending real-time message in conversation {ConversationId}", conversationId);
                await Clients.Caller.SendAsync("MessageSendError", "Failed to send message");
            }
        }

        /// <summary>
        /// Mark messages as read in real-time
        /// </summary>
        public async Task MarkConversationAsRead(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                // Log all users in conversation for read receipt
                await LogUsersInConversation(conversationId, "MarkConversationAsRead", userId.Value);

                var result = await _messageService.MarkMessagesAsReadAsync(userId.Value, Guid.Parse(conversationId));
                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("Error", result.ErrorMessage);
                    return;
                }

                var readData = result.Data!;

                // Log who will receive the read receipt
                var connectedUsers = await GetConnectedUsersInConversation(conversationId);
                _logger.LogInformation("Sending read receipt to {RecipientCount} other users in conversation {ConversationId}: [{Recipients}]",
                    connectedUsers.Count(u => u.UserId != userId.ToString()),
                    conversationId,
                    string.Join(", ", connectedUsers.Where(u => u.UserId != userId.ToString()).Select(u => u.UserId)));

                // Notify other users in conversation that messages were read
                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("MessagesMarkedAsRead", new
                    {
                        conversationId,
                        userId = userId.Value,
                        readMessageIds = readData.ReadMessageIds,
                        lastReadMessageId = readData.LastReadMessageId,
                        markedAt = readData.MarkedAt
                    });

                _logger.LogDebug("User {UserId} marked {Count} messages as read in conversation {ConversationId}",
                    userId, readData.ReadMessageIds.Count, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read in conversation {ConversationId}", conversationId);
            }
        }

        #endregion

        #region Typing Indicators

        /// <summary>
        /// User started typing in a conversation
        /// </summary>
        public async Task StartTyping(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                // Log users who will receive typing indicator
                await LogUsersInConversation(conversationId, "StartTyping", userId.Value);

                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("UserStartedTyping", new
                    {
                        userId = userId.Value,
                        conversationId,
                        timestamp = DateTime.UtcNow
                    });

                _logger.LogDebug("User {UserId} started typing in conversation {ConversationId}", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling start typing in conversation {ConversationId}", conversationId);
            }
        }

        /// <summary>
        /// User stopped typing in a conversation
        /// </summary>
        public async Task StopTyping(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                // Log users who will receive stop typing indicator
                await LogUsersInConversation(conversationId, "StopTyping", userId.Value);

                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("UserStoppedTyping", new
                    {
                        userId = userId.Value,
                        conversationId,
                        timestamp = DateTime.UtcNow
                    });

                _logger.LogDebug("User {UserId} stopped typing in conversation {ConversationId}", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling stop typing in conversation {ConversationId}", conversationId);
            }
        }

        #endregion

        #region Connection Events

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId != null)
                {
                    await _messageService.SetUserOnlineStatus(userId.Value, true);

                    // Join user to their personal notification group
                    await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId.Value));

                    _logger.LogInformation("User {UserId} connected to chat hub with connection {ConnectionId}",
                        userId, Context.ConnectionId);
                }
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on user connection");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId != null)
                {
                    await _messageService.SetUserOnlineStatus(userId.Value, true);

                    // Remove user from all conversation tracking when they disconnect
                    await CleanupUserFromAllConversations(userId.Value, Context.ConnectionId);

                    _logger.LogInformation("User {UserId} disconnected from chat hub with connection {ConnectionId}",
                        userId, Context.ConnectionId);
                }
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on user disconnection");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets current user ID from JWT claims
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             Context.User?.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        /// <summary>
        /// Logs all users currently connected to a conversation
        /// </summary>
        private async Task LogUsersInConversation(string conversationId, string eventType, Guid? currentUserId = null)
        {
            try
            {
                var connectedUsers = await GetConnectedUsersInConversation(conversationId);

                var currentUserInfo = currentUserId.HasValue ? $" (Current user: {currentUserId})" : "";

                _logger.LogInformation("[{EventType}] Conversation {ConversationId} has {ConnectedCount} connected users: [{UserList}]{CurrentUserInfo}",
                    eventType,
                    conversationId,
                    connectedUsers.Count,
                    string.Join(", ", connectedUsers.Select(u => $"{u.UserId}({u.ConnectionId})")),
                    currentUserInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log users in conversation {ConversationId} for event {EventType}",
                    conversationId, eventType);
            }
        }

        /// <summary>
        /// Gets all connected users in a specific conversation using cache tracking
        /// </summary>
        private async Task<List<ConnectedUserInfo>> GetConnectedUsersInConversation(string conversationId)
        {
            var connectedUsers = new List<ConnectedUserInfo>();

            try
            {
                var cacheKey = $"conversation_users_{conversationId}";
                var cachedUsers = await _cacheService.GetAsync<List<ConnectedUserInfo>>(cacheKey);

                if (cachedUsers != null)
                {
                    connectedUsers = cachedUsers;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get connected users for conversation {ConversationId}", conversationId);
            }

            return connectedUsers;
        }

        /// <summary>
        /// Tracks user connection to a conversation in cache
        /// </summary>
        private async Task TrackUserInConversation(string conversationId, Guid userId, string connectionId, bool isJoining)
        {
            try
            {
                var cacheKey = $"conversation_users_{conversationId}";
                var cachedUsers = await _cacheService.GetAsync<List<ConnectedUserInfo>>(cacheKey) ?? new List<ConnectedUserInfo>();

                if (isJoining)
                {
                    // Remove any existing entries for this user (in case of reconnection)
                    cachedUsers.RemoveAll(u => u.UserId == userId.ToString());

                    // Add the user
                    cachedUsers.Add(new ConnectedUserInfo
                    {
                        UserId = userId.ToString(),
                        ConnectionId = connectionId,
                        JoinedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Remove the user
                    cachedUsers.RemoveAll(u => u.UserId == userId.ToString() || u.ConnectionId == connectionId);
                }

                // Update cache with 10-minute expiry
                await _cacheService.SetAsync(cacheKey, cachedUsers, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track user {UserId} in conversation {ConversationId}", userId, conversationId);
            }
        }

        /// <summary>
        /// Cleans up user from all conversation tracking when they disconnect
        /// </summary>
        private async Task CleanupUserFromAllConversations(Guid userId, string connectionId)
        {
            try
            {
                // Get all conversation cache keys for this user
                var userConversationsKey = $"user_conversations_{userId}";
                var userConversationIds = await _cacheService.GetAsync<List<string>>(userConversationsKey);

                if (userConversationIds != null)
                {
                    foreach (var conversationId in userConversationIds)
                    {
                        await TrackUserInConversation(conversationId, userId, connectionId, false);
                    }
                }

                // Clear the user's conversation list
                await _cacheService.RemoveAsync(userConversationsKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup user {UserId} from conversations on disconnect", userId);
            }
        }

        /// <summary>
        /// Tracks which conversations a user is in
        /// </summary>
        private async Task TrackUserConversations(Guid userId, string conversationId, bool isJoining)
        {
            try
            {
                var cacheKey = $"user_conversations_{userId}";
                var userConversations = await _cacheService.GetAsync<List<string>>(cacheKey) ?? new List<string>();

                if (isJoining)
                {
                    if (!userConversations.Contains(conversationId))
                    {
                        userConversations.Add(conversationId);
                    }
                }
                else
                {
                    userConversations.Remove(conversationId);
                }

                await _cacheService.SetAsync(cacheKey, userConversations, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track conversations for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Sends notification to offline users
        /// </summary>
        private async Task SendOfflineNotification(string conversationId, Guid receiverId, MessageDTO message)
        {
            try
            {
                // Send to user's personal notification group (they might have the app open but not in this conversation)
                await Clients.Group(GetUserGroupName(receiverId))
                    .SendAsync("NewMessageNotification", message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send offline notification for conversation {ConversationId}", conversationId);
            }
        }

        /// <summary>
        /// Gets conversation group name for SignalR groups
        /// </summary>
        private static string GetConversationGroupName(string conversationId) => $"conversation_{conversationId}";

        /// <summary>
        /// Gets user group name for personal notifications
        /// </summary>
        private static string GetUserGroupName(Guid userId) => $"user_{userId}";

        #endregion

        #region Admin/Monitoring Methods (Optional)

        /// <summary>
        /// Gets connection info for monitoring (admin only)
        /// </summary>
        public async Task GetConnectionInfo()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                var connectionInfo = new
                {
                    userId = userId.Value,
                    connectionId = Context.ConnectionId,
                    userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString(),
                    ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
                    connectedAt = DateTime.UtcNow
                };

                await Clients.Caller.SendAsync("ConnectionInfo", connectionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection info");
            }
        }

        /// <summary>
        /// Ping method for connection health checks
        /// </summary>
        public async Task Ping()
        {
            try
            {
                await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ping");
            }
        }

        #endregion

        #region Data Models

        /// <summary>
        /// Information about a connected user
        /// </summary>
        private class ConnectedUserInfo
        {
            public string UserId { get; set; } = string.Empty;
            public string ConnectionId { get; set; } = string.Empty;
            public DateTime JoinedAt { get; set; }
        }

        #endregion
    }
}