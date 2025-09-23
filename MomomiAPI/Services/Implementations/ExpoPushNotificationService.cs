using MomomiAPI.Services.Interfaces;
using Newtonsoft.Json;
using System.Text;

namespace MomomiAPI.Services.Implementations
{
    public class ExpoPushNotificationService : IPushNotificationService
    {
        private readonly HttpClient _httpClient;
        private const string ExpoPushSendEndpoint = "https://exp.host/--/api/v2/push/send";

        public ExpoPushNotificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendNewMatchNotificationAsync(string deviceToken, string matchName)
        {
            var payload = new
            {
                to = deviceToken,
                title = "It's a Match! 🎉",
                body = $"You and {matchName} have matched.",
                data = new
                {
                    type = "newMatch",
                    matchName = matchName
                }
            };

            await SendNotificationAsync(payload);
        }
        public async Task SendNewMessageNotificationAsync(string deviceToken, string senderName, string message)
        {
            var payload = new
            {
                to = deviceToken,
                title = $"New Message from {senderName}",
                body = message,
                data = new
                {
                    type = "newMessage",
                    sender = senderName
                }
            };

            await SendNotificationAsync(payload);
        }

        private async Task SendNotificationAsync(object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ExpoPushSendEndpoint, content);
            response.EnsureSuccessStatusCode();

            // You can optionally read and process the response for Expo Push Tickets
            // and receipts, which contain information about delivery status.
            // var responseBody = await response.Content.ReadAsStringAsync();
            // var result = JsonConvert.DeserializeObject<PushTicketResponse>(responseBody);
            // This is important for handling errors like invalid tokens.
        }
    }
}
