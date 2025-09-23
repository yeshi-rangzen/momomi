using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class FirebasePushNotificationService : IPushNotificationService
    {
        public Task SendNewMatchNotificationAsync(string deviceToken, string matchName)
        {
            throw new NotImplementedException();
        }

        public Task SendNewMessageNotificationAsync(string deviceToken, string senderName, string message)
        {
            throw new NotImplementedException();
        }
    }
}
