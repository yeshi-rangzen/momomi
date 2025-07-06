using Microsoft.EntityFrameworkCore;
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

        public async Task<bool> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, object? data = null)
        {
            try
            {
                // Check if user has notifications enabled
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null || !user.NotificationsEnabled)
                {
                    _logger.LogDebug("Notifications disabled for user {UserId}", userId);
                    return false;
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
                    await SendPushNotificationAsync(user.PushToken, title, message, data);
                    notification.IsSent = true;
                    notification.SentAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Notification sent to user {UserId}: {Title}", userId, title);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> SendMatchNotificationAsync(Guid userId, Guid matchedUserId)
        {
            try
            {
                var matchedUser = await _dbContext.Users.FindAsync(matchedUserId);
                if (matchedUser == null) return false;

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
                return false;
            }
        }

        public async Task<bool> SendSuperLikeNotificationAsync(Guid userId, Guid superLikerUserId)
        {
            try
            {
                var superLiker = await _dbContext.Users.FindAsync(superLikerUserId);
                if (superLiker == null) return false;

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
                return false;
            }
        }

        public async Task<bool> SendMessageNotificationAsync(Guid userId, Guid senderId, string messagePreview)
        {
            try
            {
                var sender = await _dbContext.Users.FindAsync(senderId);
                if (sender == null) return false;

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
                return false;
            }
        }

        public async Task<List<NotificationDTO>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            try
            {
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

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> MarkNotificationAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var notification = await _dbContext.PushNotifications
                    .FirstOrDefaultAsync(pn => pn.Id == notificationId && pn.UserId == userId);

                if (notification == null) return false;

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
                return false;
            }
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(Guid userId)
        {
            try
            {
                var unreadNotifications = await _dbContext.PushNotifications
                    .Where(pn => pn.UserId == userId && !pn.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                return false;
            }
        }

        public async Task<int> GetUnreadNotificationCountAsync(Guid userId)
        {
            try
            {
                return await _dbContext.PushNotifications
                    .CountAsync(pn => pn.UserId == userId && !pn.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> UpdateUserPushTokenAsync(Guid userId, string pushToken)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return false;

                user.PushToken = pushToken;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated push token for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating push token for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> EnableNotificationsAsync(Guid userId, bool enabled)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null) return false;

                user.NotificationsEnabled = enabled;
                user.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Set notifications {Status} for user {UserId}",
                    enabled ? "enabled" : "disabled", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification settings for user {UserId}", userId);
                return false;
            }
        }

        private async Task<bool> SendPushNotificationAsync(string pushToken, string title, string message, object? data = null)
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
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to send push notification. Status: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to token {Token}", pushToken[..8] + "...");
                return false;
            }
        }
    }
}
