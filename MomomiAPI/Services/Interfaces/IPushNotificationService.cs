namespace MomomiAPI.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task SendNewMessageNotificationAsync(string deviceToken, string senderName, string message);
        Task SendNewMatchNotificationAsync(string deviceToken, string matchName);
        Task SendLikeNotificationAsync(string deviceToken, string senderName);
    }
}
