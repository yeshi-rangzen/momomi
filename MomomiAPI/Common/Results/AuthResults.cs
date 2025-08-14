using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;
using static MomomiAPI.Services.Implementations.AuthService;

namespace MomomiAPI.Common.Results
{
    /// Email verification-specific data
    public class EmailVerificationData
    {
        public string? VerificationToken { get; set; }
        public bool IsEmailRegistered { get; set; }
        public int? RemainingAttempts { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string Email { get; set; } = string.Empty;
        public VerificationType Type { get; set; }
        public DateTime SentAt { get; set; }
    }

    public enum VerificationType
    {
        CodeSent,
        CodeVerified,
        CodeExpired,
        CodeInvalid,
        RateLimitExceeded,
        ValidationError,
        Error
    }
    public class EmailVerificationResult : OperationResult<EmailVerificationData>
    {
        private EmailVerificationResult(bool success, EmailVerificationData? data, string? errorCode,
              string? errorMessage, Dictionary<string, object>? metadata = null)
              : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static EmailVerificationResult CodeSentSuccessfully(string verificationToken,
            DateTime expiresAt, int remainingAttempts, bool isEmailRegistered, string email)
        {
            var data = new EmailVerificationData
            {
                VerificationToken = verificationToken,
                ExpiresAt = expiresAt,
                RemainingAttempts = remainingAttempts,
                IsEmailRegistered = isEmailRegistered,
                Email = email,
                Type = VerificationType.CodeSent,
                SentAt = DateTime.UtcNow
            };

            return new EmailVerificationResult(true, data, null, null);
        }

        public static EmailVerificationResult CodeVerifiedSuccessfully(string verificationToken,
            DateTime expiresAt, bool isEmailRegistered, string email)
        {
            var data = new EmailVerificationData
            {
                VerificationToken = verificationToken,
                ExpiresAt = expiresAt,
                IsEmailRegistered = isEmailRegistered,
                Email = email,
                Type = VerificationType.CodeVerified,
                SentAt = DateTime.UtcNow
            };

            return new EmailVerificationResult(true, data, null, null);
        }

        public static EmailVerificationResult RateLimitExceeded(int remainingAttempts, string email)
        {
            var data = new EmailVerificationData
            {
                RemainingAttempts = remainingAttempts,
                IsEmailRegistered = false,
                Email = email,
                Type = VerificationType.RateLimitExceeded
            };

            return new EmailVerificationResult(false, data, ErrorCodes.RATE_LIMIT_EXCEEDED,
                "Rate limit exceeded. Please try again later.");
        }

        public static EmailVerificationResult InvalidOtpCode(int remainingAttempts, string email)
        {
            var data = new EmailVerificationData
            {
                RemainingAttempts = remainingAttempts,
                Email = email,
                Type = VerificationType.CodeInvalid
            };

            return new EmailVerificationResult(false, data, ErrorCodes.INVALID_INPUT,
                $"Invalid OTP code. {remainingAttempts} attempts remaining.");
        }

        public static EmailVerificationResult OtpCodeExpired(string email)
        {
            var data = new EmailVerificationData
            {
                Email = email,
                Type = VerificationType.CodeExpired,
                RemainingAttempts = 0
            };

            return new EmailVerificationResult(false, data, ErrorCodes.OTP_TOKEN_EXPIRED,
                "OTP code has expired. Please request a new one.");
        }

        public static EmailVerificationResult ValidationError(string message, string email)
        {
            var data = new EmailVerificationData
            {
                Email = email,
                RemainingAttempts = 0,
                Type = VerificationType.ValidationError
            };

            return new EmailVerificationResult(false, data, ErrorCodes.VALIDATION_ERROR, message);
        }

        public static EmailVerificationResult Error(string message, string email)
        {
            var data = new EmailVerificationData
            {
                Email = email,
                RemainingAttempts = 0,
                Type = VerificationType.Error,
            };

            return new EmailVerificationResult(false, data, ErrorCodes.INTERNAL_SERVER_ERROR,
                $"Email verification failed: {message}");
        }
    }

    /// Login-specific data that extends beyond just user information
    public class LoginData
    {
        public UserDTO User { get; set; } = null!;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }

    public class LoginResult : OperationResult<LoginData>
    {
        private LoginResult(bool success, LoginData? data, string? errorCode,
           string? errorMessage, Dictionary<string, object>? metadata = null)
           : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static LoginResult Successful(UserDTO user, string accessToken, string refreshToken,
             DateTime tokenExpiresAt, Dictionary<string, object>? metadata = null)
        {
            var loginData = new LoginData
            {
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt,
                LastLoginAt = DateTime.UtcNow
            };

            return new LoginResult(true, loginData, null, null, metadata);
        }
        public static LoginResult InvalidCredentials()
            => new(false, null, ErrorCodes.INVALID_CREDENTIALS,
                "Invalid email or verification code");

        public static LoginResult InvalidToken()
            => new(false, null, ErrorCodes.INVALID_CREDENTIALS,
                "Invalid access or refresh token");

        public static LoginResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "User not found. Please register first");

        public static LoginResult AccountInactive()
            => new(false, null, ErrorCodes.UNAUTHORIZED,
                "Account is inactive. Please contact support");

        public static LoginResult Error(string errorMessage)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR,
                $"Login failed: {errorMessage}");
    }

    /// Registration-specific data
    public class RegistrationData
    {
        public UserDTO User { get; set; } = null!;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
    }

    public class RegistrationResult : OperationResult<RegistrationData>
    {
        private RegistrationResult(bool success, RegistrationData? data, string? errorCode, string? errorMessage,
                    Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static RegistrationResult Successful(UserDTO user, string accessToken, string refreshToken,
            DateTime tokenExpiresAt, Dictionary<string, object>? metadata = null)
        {
            var registrationData = new RegistrationData
            {
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt,
            };

            return new RegistrationResult(true, registrationData, null, null, metadata);
        }

        public static RegistrationResult EmailAlreadyRegistered()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "Email already registered. Please login instead");

        public static RegistrationResult InvalidVerificationToken()
            => new(false, null, ErrorCodes.TOKEN_EXPIRED,
                "Invalid or expired verification token. Please verify your email again");

        public static RegistrationResult UnderageUser()
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                "You must be at least 18 years old to register");

        public static RegistrationResult ValidationError(string errorMessage)
            => new(false, null, ErrorCodes.VALIDATION_ERROR, errorMessage);

        public static RegistrationResult Error(string errorMessage)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR,
                $"Registration failed: {errorMessage}");
    }

    /// Refresh token-specific data
    public class RefreshTokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
    }
    public class RefreshTokenResult : OperationResult<RefreshTokenData>
    {
        private RefreshTokenResult(bool success, RefreshTokenData? data, string? errorCode,
             string? errorMessage, Dictionary<string, object>? metadata = null)
             : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static RefreshTokenResult RefreshSuccess(string accessToken, string refreshToken,
            DateTime tokenExpiresAt, Dictionary<string, object>? metadata = null)
        {
            var refreshData = new RefreshTokenData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenExpiresAt = tokenExpiresAt,
            };

            return new RefreshTokenResult(true, refreshData, null, null, metadata);
        }

        public static RefreshTokenResult InvalidToken(string message)
            => new(false, null, ErrorCodes.INVALID_CREDENTIALS, message);

        public static RefreshTokenResult UserNotFound(string message)
            => new(false, null, ErrorCodes.USER_NOT_FOUND, message);

        public static RefreshTokenResult TokensRevoked(string message)
            => new(false, null, ErrorCodes.TOKEN_EXPIRED, message);

        public static RefreshTokenResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    public class ResendCooldownResult
    {
        public bool CanResend { get; set; }
        public string? ErrorMessage { get; set; }

        public static ResendCooldownResult CanResendOTP()
            => new() { CanResend = true };

        public static ResendCooldownResult InCooldown(string errorMessage)
            => new() { CanResend = false, ErrorMessage = errorMessage };
    }

    public class OtpValidationResult
    {
        public bool IsValid { get; set; }
        public OtpAttemptData? AttemptData { get; set; }

        public static OtpValidationResult Valid(OtpAttemptData attemptData)
            => new() { IsValid = true, AttemptData = attemptData };

        public static OtpValidationResult Invalid()
            => new() { IsValid = false };
    }

}
