using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using System.Text.Json;

namespace MomomiAPI.Services.Implementations
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly HttpClient _httpClient;

        public PushNotificationService(
            MomomiDbContext dbContext,
            ILogger<PushNotificationService> logger,
            HttpClient httpClient)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<OperationResult> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, object? data = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    return OperationResult.ValidationFailure("Notification title cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return OperationResult.ValidationFailure("Notification message cannot be empty.");
                }

                // Check if user has notifications enabled
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }
                if (!user.NotificationsEnabled)
                {
                    _logger.LogDebug("Notifications disabled for user {UserId}", userId);
                    return OperationResult.BusinessRuleViolation("User has disabled notifications.");
                }


                // Create notification record
                var notification = new PushNotification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NotificationType = type,
                    Data = data != null ? JsonSerializer.Serialize(data) : null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.PushNotifications.Add(notification);

                // Send push notification if user has a push token
                if (!string.IsNullOrEmpty(user.PushToken))
                {
                    var pushResult = await SendPushNotificationAsync(user.PushToken, title, message, data);
                    if (pushResult.Success)
                    {
                        notification.IsSent = true;
                        notification.SentAt = DateTime.UtcNow;
                    }
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Notification sent to user {UserId}: {Title}", userId, title);
                return OperationResult.Successful()
                    .WithMetadata("notification_id", notification.Id)
                    .WithMetadata("sent_at", notification.SentAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
                return OperationResult.Failed("Unable to send notification.");
            }
        }

        public async Task<OperationResult> SendMatchNotificationAsync(Guid userId, Guid matchedUserId)
        {
            try
            {
                var matchedUser = await _dbContext.Users.FindAsync(matchedUserId);
                if (matchedUser == null)
                {
                    return OperationResult.NotFound("Matched user not found.");
                }

                var title = "🎉 New Match!";
                var message = $"You and {matchedUser.FirstName} liked each other!";
                var data = new
                {
                    type = "match",
                    matchedUserId = matchedUserId,
                    matchedUserName = matchedUser.FirstName
                };

                return await SendNotificationAsync(userId, title, message, NotificationType.Match, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending match notification to user {UserId}", userId);
                return OperationResult.Failed("Unable to send match notification.");
            }
        }

        public async Task<OperationResult> SendSuperLikeNotificationAsync(Guid userId, Guid superLikerUserId)
        {
            try
            {
                var superLiker = await _dbContext.Users.FindAsync(superLikerUserId);
                if (superLiker == null)
                {
                    return OperationResult.NotFound("Super liker user not found.");
                }

                var title = "⭐ Super Like!";
                var message = $"{superLiker.FirstName} super liked you!";
                var data = new
                {
                    type = "super_like",
                    superLikerUserId = superLikerUserId,
                    superLikerName = superLiker.FirstName
                };

                return await SendNotificationAsync(userId, title, message, NotificationType.SuperLike, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending super like notification to user {UserId}", userId);
                return OperationResult.Failed("Unable to send super like notification.");
            }
        }

        public async Task<OperationResult> SendMessageNotificationAsync(Guid userId, Guid senderId, string messagePreview)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(messagePreview))
                {
                    return OperationResult.ValidationFailure("Message preview cannot be empty.");
                }

                var sender = await _dbContext.Users.FindAsync(senderId);
                if (sender == null)
                {
                    return OperationResult.NotFound("Sender user not found.");
                }

                var title = $"Message from {sender.FirstName}";
                var message = messagePreview.Length > 50 ? messagePreview[..50] + "..." : messagePreview;
                var data = new
                {
                    type = "message",
                    senderId = senderId,
                    senderName = sender.FirstName
                };

                return await SendNotificationAsync(userId, title, message, NotificationType.Message, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message notification to user {UserId}", userId);
                return OperationResult.Failed("Unable to send message notification.");
            }
        }

        public async Task<OperationResult<List<NotificationDTO>>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            try
            {
                if (page < 1)
                {
                    return OperationResult<List<NotificationDTO>>.ValidationFailure("Page must be greater than 0.");
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    return OperationResult<List<NotificationDTO>>.ValidationFailure("Page size must be between 1 and 100.");
                }

                var notifications = await _dbContext.PushNotifications
                    .Where(pn => pn.UserId == userId)
                    .OrderByDescending(pn => pn.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(pn => new NotificationDTO
                    {
                        Id = pn.Id,
                        Title = pn.Title,
                        Message = pn.Message,
                        NotificationType = pn.NotificationType,
                        Data = pn.Data,
                        IsRead = pn.IsRead,
                        CreatedAt = pn.CreatedAt,
                        ReadAt = pn.ReadAt
                    })
                    .ToListAsync();

                return OperationResult<List<NotificationDTO>>.Successful(notifications)
                   .WithMetadata("page", page)
                   .WithMetadata("page_size", pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
                return OperationResult<List<NotificationDTO>>.Failed("Unable to retrieve notifications.");
            }
        }

        public async Task<OperationResult> MarkNotificationAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var notification = await _dbContext.PushNotifications
                    .FirstOrDefaultAsync(pn => pn.Id == notificationId && pn.UserId == userId);

                if (notification == null)
                {
                    return OperationResult.NotFound("Notification not found.");
                }

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return OperationResult.Successful()
                                    .WithMetadata("read_at", notification.ReadAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
                return OperationResult.Failed("Unable to mark notification as read.");
            }
        }

        public async Task<OperationResult> MarkAllNotificationsAsReadAsync(Guid userId)
        {
            try
            {
                var unreadNotifications = await _dbContext.PushNotifications
                    .Where(pn => pn.UserId == userId && !pn.IsRead)
                    .ToListAsync();

                if (!unreadNotifications.Any())
                {
                    return OperationResult.BusinessRuleViolation("No unread notifications found.");
                }

                var readAt = DateTime.UtcNow;
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = readAt;
                }

                await _dbContext.SaveChangesAsync();
                return OperationResult.Successful()
                    .WithMetadata("notifications_marked", unreadNotifications.Count)
                    .WithMetadata("read_at", readAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                return OperationResult.Failed("Unable to mark all notifications as read.");
            }
        }

        public async Task<OperationResult<int>> GetUnreadNotificationCountAsync(Guid userId)
        {
            try
            {
                var count = await _dbContext.PushNotifications
                   .CountAsync(pn => pn.UserId == userId && !pn.IsRead);

                return OperationResult<int>.Successful(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count for user {UserId}", userId);
                return OperationResult<int>.Failed("Unable to get unread notification count.");
            }
        }

        public async Task<OperationResult> UpdateUserPushTokenAsync(Guid userId, string pushToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pushToken))
                {
                    return OperationResult.ValidationFailure("Push token cannot be empty.");
                }

                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }

                user.PushToken = pushToken;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated push token for user {UserId}", userId);
                return OperationResult.Successful()
                                    .WithMetadata("updated_at", user.UpdatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating push token for user {UserId}", userId);
                return OperationResult.Failed("Unable to update push token.");
            }
        }

        public async Task<OperationResult> EnableNotificationsAsync(Guid userId, bool enabled)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }

                user.NotificationsEnabled = enabled;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Set notifications {Status} for user {UserId}",
                    enabled ? "enabled" : "disabled", userId);
                return OperationResult.Successful()
                                    .WithMetadata("notifications_enabled", enabled)
                                    .WithMetadata("updated_at", user.UpdatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings for user {UserId}", userId);
                return OperationResult.Failed("Unable to update notification settings.");
            }
        }

        private async Task<OperationResult> SendPushNotificationAsync(string pushToken, string title, string message, object? data = null)
        {
            try
            {
                // This is a simplified implementation
                // In production, you would integrate with:
                // - Firebase Cloud Messaging (FCM) for Android
                // - Apple Push Notification Service (APNs) for iOS
                // - Expo Push Notifications for Expo apps

                var payload = new
                {
                    to = pushToken,
                    title = title,
                    body = message,
                    data = data,
                    sound = "default"
                };

                // Example: Expo Push Notifications
                var response = await _httpClient.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", payload);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Push notification sent successfully to token {Token}", pushToken[..8] + "...");
                    return OperationResult.Successful();
                }
                else
                {
                    _logger.LogWarning("Failed to send push notification. Status: {StatusCode}", response.StatusCode);
                    return OperationResult.Failed($"Push notification failed with status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to token {Token}", pushToken[..8] + "...");
                return OperationResult.Failed("Push notification delivery failed.");
            }
        }
    }
}
