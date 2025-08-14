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

        // Cache TTL for online status
        private static readonly TimeSpan OnlineStatusTTL = TimeSpan.FromMinutes(5);

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
        public async Task JoinConversationRoom(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    await Clients.Caller.SendAsync("Error", "User not authenticated");
                    return;
                }

                // Verify user has access to this conversation
                var conversationResult = await _messageService.GetConversationDetailsAsync(userId.Value, Guid.Parse(conversationId));
                if (!conversationResult.Success)
                {
                    await Clients.Caller.SendAsync("Error", "Access denied to conversation");
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));

                // Update user's online status
                await SetUserOnlineStatus(userId.Value, true);

                // Notify other users in conversation that this user is online
                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("UserJoinedConversation", userId.Value);

                _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining conversation {ConversationId}", conversationId);
                await Clients.Caller.SendAsync("Error", "Failed to join conversation");
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

                // Send to all users in the conversation room
                await Clients.Group(GetConversationGroupName(conversationId))
                    .SendAsync("MessageReceived", new
                    {
                        message = messageData.Message,
                        isFirstMessage = messageData.IsFirstMessage,
                        messageCount = messageData.ConversationMessageCount
                    });

                // Send push notification to offline users
                await SendOfflineNotification(conversationId, messageData.ReceiverId, messageData.Message);

                _logger.LogDebug("Real-time message {MessageId} sent in conversation {ConversationId}",
                    messageData.Message.Id, conversationId);
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

                var result = await _messageService.MarkMessagesAsReadAsync(userId.Value, Guid.Parse(conversationId));
                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("Error", result.ErrorMessage);
                    return;
                }

                var readData = result.Data!;

                // Notify other users in conversation that messages were read
                await Clients.OthersInGroup(GetConversationGroupName(conversationId))
                    .SendAsync("MessagesMarkedAsRead", new
                    {
                        conversationId,
                        userId = userId.Value,
                        messagesMarkedCount = readData.MessagesMarkedCount,
                        lastReadMessageId = readData.LastReadMessageId,
                        markedAt = readData.MarkedAt
                    });

                _logger.LogDebug("User {UserId} marked {Count} messages as read in conversation {ConversationId}",
                    userId, readData.MessagesMarkedCount, conversationId);
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
                    await SetUserOnlineStatus(userId.Value, true);

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
                    await SetUserOnlineStatus(userId.Value, false);

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
        /// Sets user online/offline status in cache
        /// </summary>
        private async Task SetUserOnlineStatus(Guid userId, bool isOnline)
        {
            try
            {
                var cacheKey = $"user_online_{userId}";

                if (isOnline)
                {
                    await _cacheService.SetStringAsync(cacheKey, "true", OnlineStatusTTL);
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

        /// <summary>
        /// Sends notification to offline users
        /// </summary>
        private async Task SendOfflineNotification(string conversationId, Guid receiverId, MessageDTO message)
        {
            try
            {
                // Check if receiver is online
                var isReceiverOnline = await _messageService.IsUserOnlineAsync(receiverId);

                if (!isReceiverOnline)
                {
                    // Send to user's personal notification group (they might have the app open but not in this conversation)
                    await Clients.Group(GetUserGroupName(receiverId))
                        .SendAsync("NewMessageNotification", new
                        {
                            conversationId,
                            message = new
                            {
                                id = message.Id,
                                senderName = message.SenderName,
                                content = message.Content.Length > 50 ?
                                    message.Content.Substring(0, 47) + "..." :
                                    message.Content,
                                sentAt = message.SentAt
                            }
                        });
                }
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
    }
}