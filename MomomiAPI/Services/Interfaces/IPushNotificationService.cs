using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Enums;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task<OperationResult> SendNotificationAsync(Guid userId, string title, string message, NotificationType type, object? data = null);
        Task<OperationResult> SendMatchNotificationAsync(Guid userId, Guid matchedUserId);
        Task<OperationResult> SendSuperLikeNotificationAsync(Guid userId, Guid superLikerUserId);
        Task<OperationResult> SendMessageNotificationAsync(Guid userId, Guid senderId, string messagePreview);
        Task<OperationResult<List<NotificationDTO>>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20);
        Task<OperationResult> MarkNotificationAsReadAsync(Guid notificationId, Guid userId);
        Task<OperationResult> MarkAllNotificationsAsReadAsync(Guid userId);
        Task<OperationResult<int>> GetUnreadNotificationCountAsync(Guid userId);
        Task<OperationResult> UpdateUserPushTokenAsync(Guid userId, string pushToken);
        Task<OperationResult> EnableNotificationsAsync(Guid userId, bool enabled);
    }
}
