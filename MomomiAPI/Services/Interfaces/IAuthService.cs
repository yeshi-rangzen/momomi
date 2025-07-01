using MomomiAPI.Models.Entities;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IAuthService
    {
        // OTP-based authentication methods
        Task<OtpResult> SendOtpAsync(SendOtpRequest request);
        Task<AuthResult> VerifyOtpAndLoginAsync(LoginWithOtpRequest request);
        Task<AuthResult> RegisterWithOtpAsync(RegisterWithOtpRequest request);
        Task<OtpResult> ResendOtpAsync(ResendOtpRequest request);

        // Token and user management
        Task<User?> GetUserFromTokenAsync(string accessToken);
        Task<bool> LogoutAsync(string accessToken);
        Task<AuthResult> RefreshTokenAsync(string refreshToken);

        // Utility methods
        Task<bool> IsEmailRegisteredAsync(string email);
        Task<bool> RevokeUserSessionsAsync(Guid userId);
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public User? User { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class OtpResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? RemainingAttempts { get; set; }
    }
}
