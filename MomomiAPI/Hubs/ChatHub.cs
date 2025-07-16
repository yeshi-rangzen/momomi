using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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

        public async Task JoinConversation(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

                // Track online users
                await _cacheService.SetStringAsync($"user_online_{userId}", "true", TimeSpan.FromMinutes(5));

                _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining conversation {ConversationId} for user {UserId}", conversationId, Context.UserIdentifier);
            }
        }

        public async Task LeaveConversation(string conversationId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
                _logger.LogInformation("User left conversation {ConversationId}", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving conversation {ConversationId} for user {UserId}", conversationId, Context.UserIdentifier);
            }
        }
        public async Task SendMessage(string conversationId, string content, string messageType = "text")
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

                var messageDto = await _messageService.SendMessageAsync(userId.Value, request);
                if (messageDto == null)
                {
                    await Clients.Caller.SendAsync("Error", "Failed to send message");
                    return;
                }

                // Send to all users in the conversation
                await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", messageDto);

                // Send notification to the other user if they're not in the conversation
                var conversationResult = await _messageService.GetConversationAsync(userId.Value, Guid.Parse(conversationId));
                var conversation = conversationResult.Data;

                if (conversation != null)
                {
                    var isOtherUserOnline = await _cacheService.ExistsAsync($"user_online_{conversation.OtherUserId}");
                    if (!isOtherUserOnline)
                    {
                        await Clients.User(conversation.OtherUserId.ToString()).SendAsync("NewMessageNotification", messageDto);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in conversation {ConversationId} for user {UserId}", conversationId, Context.UserIdentifier);
            }
        }

        public async Task MarkMessageAsRead(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                await _messageService.MarkMessagesAsReadAsync(userId.Value, Guid.Parse(conversationId));

                // Notify other user that messages were read
                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("MessagesMarkedAsRead", conversationId, userId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as read in conversation {ConversationId} for user {UserId}", conversationId, Context.UserIdentifier);
            }
        }

        public async Task StartTyping(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserStartedTyping", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting typing in conversation {ConversationId} for user {UserId}", conversationId, Context.UserIdentifier);
            }
        }

        public async Task StopTyping(string conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null) return;

                await Clients.OthersInGroup($"conversation_{conversationId}")
                    .SendAsync("UserStoppedTyping", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping typing in conversation {ConversationId}", conversationId);
            }
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId != null)
                {
                    await _cacheService.SetStringAsync($"user_online_{userId}", "true", TimeSpan.FromMinutes(5));
                    _logger.LogInformation("User {UserId} connected to chat hub", userId);
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
                    await _cacheService.RemoveAsync($"user_online_{userId}");
                    _logger.LogInformation("User {UserId} disconnected from chat hub", userId);
                }
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on user disconnection");
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
