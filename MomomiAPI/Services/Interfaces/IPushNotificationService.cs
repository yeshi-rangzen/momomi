using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task<bool> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, object? data = null);
        Task<bool> SendMatchNotificationAsync(Guid userId, Guid matchedUserId);
        Task<bool> SendSuperLikeNotificationAsync(Guid userId, Guid superLikerUserId);
        Task<bool> SendMessageNotificationAsync(Guid userId, Guid senderId, string messagePreview);
        Task<List<NotificationDTO>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20);
        Task<bool> MarkNotificationAsReadAsync(Guid notificationId, Guid userId);
        Task<bool> MarkAllNotificationsAsReadAsync(Guid userId);
        Task<int> GetUnreadNotificationCountAsync(Guid userId);
        Task<bool> UpdateUserPushTokenAsync(Guid userId, string pushToken);
        Task<bool> EnableNotificationsAsync(Guid userId, bool enabled);
    }
}
