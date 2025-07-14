using MomomiAPI.Models.Entities;

namespace MomomiAPI.Common.Results
{
    public class EmailVerificationResult : OperationResult<string>
    {
        public DateTime? ExpiresAt { get; private set; }
        public int? RemainingAttempts { get; private set; }

        private EmailVerificationResult(bool success, string? data, string? errorMessage, DateTime? expiresAt, int? remainingAttempts)
            : base(success, data, errorMessage)
        {
            ExpiresAt = expiresAt;
            RemainingAttempts = remainingAttempts;
        }

        public static EmailVerificationResult CodeSentSuccessfully(string verificationToken, DateTime expiresAt, int remainingAttempts)
           => new(true, verificationToken, null, expiresAt, remainingAttempts);

        public static EmailVerificationResult CodeVerifiedSuccessfully(string verificationToken, DateTime expiresAt)
            => new(true, verificationToken, null, expiresAt, null);

        public static EmailVerificationResult RateLimitExceeded(int remainingAttempts)
            => new(false, null, "Rate limit exceeded. Please try again later.", null, remainingAttempts);

        public static EmailVerificationResult InvalidCode(int remainingAttempts)
            => new(false, null, $"Invalid verification code. {remainingAttempts} attempts remaining.", null, remainingAttempts);

        public static EmailVerificationResult CodeExpired()
            => new(false, null, "Verification code has expired. Please request a new one.", null, 0);
    }

    public class LoginResult : OperationResult<User>
    {
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public DateTime? TokenExpiresAt { get; private set; }

        private LoginResult(bool success, User? user, string? errorMessage, string? accessToken, string? refreshToken, DateTime? tokenExpiresAt)
            : base(success, user, errorMessage)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            TokenExpiresAt = tokenExpiresAt;
        }

        public static new LoginResult Success(User user, string accessToken, string refreshToken, DateTime tokenExpiresAt)
            => new(true, user, null, accessToken, refreshToken, tokenExpiresAt);

        public static LoginResult InvalidCredentials()
            => new(false, null, "Invalid email or verification code.", null, null, null);

        public static LoginResult UserNotFound()
            => new(false, null, "User not found. Please register first.", null, null, null);

        public static LoginResult AccountInactive()
            => new(false, null, "Account is inactive. Please contact support.", null, null, null);
    }

    public class RegistrationResult : OperationResult<User>
    {
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public DateTime? TokenExpiresAt { get; private set; }

        private RegistrationResult(bool success, User? user, string? errorMessage, string? accessToken, string? refreshToken, DateTime? tokenExpiresAt)
            : base(success, user, errorMessage)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            TokenExpiresAt = tokenExpiresAt;
        }

        public static new RegistrationResult Success(User user, string accessToken, string refreshToken, DateTime tokenExpiresAt)
            => new(true, user, null, accessToken, refreshToken, tokenExpiresAt);

        public static RegistrationResult EmailAlreadyRegistered()
            => new(false, null, "Email already registered. Please login instead.", null, null, null);

        public static RegistrationResult InvalidVerificationToken()
            => new(false, null, "Invalid or expired verification token. Please verify your email again.", null, null, null);

        public static RegistrationResult UnderageUser()
            => new(false, null, "You must be at least 18 years old to register.", null, null, null);
    }
}
