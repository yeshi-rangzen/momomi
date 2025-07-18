using System.Text.Json.Serialization;

namespace MomomiAPI.Models
{
    public class OtpAttemptInfo
    {
        public string Email { get; set; }
        public int AttemptCount { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }

        [JsonConstructor]
        public OtpAttemptInfo(string email, int attemptCount, DateTime sentAt, DateTime expiresAt)
        {
            Email = email;
            AttemptCount = attemptCount;
            SentAt = sentAt;
            ExpiresAt = expiresAt;
        }
    }
}
