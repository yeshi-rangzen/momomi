using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models;
using MomomiAPI.Services.Interfaces;
using Supabase.Gotrue;

namespace MomomiAPI.Services.Implementations
{
    public class EmailVerificationService : IEmailVerificationService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ICacheService _cacheService;
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger _logger;

        public EmailVerificationService(Supabase.Client supabaseClient, ICacheService cacheService, MomomiDbContext dbContext, ILogger<EmailVerificationService> logger)
        {
            _supabaseClient = supabaseClient;
            _cacheService = cacheService;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<EmailVerificationResult> SendVerificationCode(string email)
        {
            try
            {
                _logger.LogInformation("Sending verification code to {Email}", email);

                // Check rate limiting
                var rateLimitKey = CacheKeys.Authentication.OtpRateLimit(email);
                var rateLimitCount = await _cacheService.GetAsync<int?>(rateLimitKey);

                if (rateLimitCount >= AppConstants.Limits.MaxOtpRequestsPerHour)
                {
                    return EmailVerificationResult.RateLimitExceeded(0);
                }

                // Send OTP via Supabase
                var otpResponse = await _supabaseClient.Auth.SignInWithOtp(
                    options: new SignInWithPasswordlessEmailOptions(email)
                    {
                        Data = new Dictionary<string, object>
                        {
                            {"app_name", "mimori" },
                            {"purpose", "email_verification" }
                        }
                    }
                );

                if (otpResponse == null)
                {
                    return (EmailVerificationResult)EmailVerificationResult.Failed("Failed to send verification code. Please try again.");
                }

                // Update rate limiting
                await _cacheService.SetAsync(rateLimitKey, rateLimitCount + 1, CacheKeys.Duration.RateLimit);

                // Store OTP attempt info for validation
                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var expiresAt = DateTime.UtcNow.AddMinutes(10);
                var otpAttemptInfo = new OtpAttemptInfo(email, 0, DateTime.UtcNow, expiresAt);

                await _cacheService.SetAsync(otpAttemptKey, otpAttemptInfo, CacheKeys.Duration.OtpAttempt);

                _logger.LogInformation("Verification code sent successfully to {Email}", email);

                return EmailVerificationResult.CodeSentSuccessfully(
                    Guid.NewGuid().ToString(), // Temporary token for this session
                    expiresAt,
                    AppConstants.Limits.MaxOtpAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification code to {Email}", email);
                return EmailVerificationResult.Failed("Failed to send verification code. Please try again.");
            }
        }

        public async Task<EmailVerificationResult> VerifyEmailCode(string email, string code)
        {
            try
            {
                _logger.LogInformation("Verifying email code for {Email}", email);

                // Check OTP attempt info
                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

                if (otpAttemptInfo == null)
                {
                    return EmailVerificationResult.CodeExpired();
                }

                //var expiresAt = otpAttemptInfo.GetProperty("ExpiresAt").GetDateTime();
                var expiresAt = otpAttemptInfo.ExpiresAt;
                var attemptCount = otpAttemptInfo.AttemptCount;
                //var attemptCount = otpAttemptInfo.GetProperty("AttemptCount").GetInt32();

                if (expiresAt < DateTime.UtcNow)
                {
                    return EmailVerificationResult.CodeExpired();
                }

                if (attemptCount >= AppConstants.Limits.MaxOtpAttempts)
                {
                    return EmailVerificationResult.InvalidCode(0);
                }

                // Verify the OTP using Supabase Auth
                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Email);

                if (verifyResponse?.User == null)
                {
                    // Increment attempt count
                    var updatedAttemptInfo = new OtpAttemptInfo(email, attemptCount + 1, otpAttemptInfo.SentAt, expiresAt);

                    await _cacheService.SetAsync(otpAttemptKey, updatedAttemptInfo,
                                            TimeSpan.FromMinutes((expiresAt - DateTime.UtcNow).TotalMinutes));

                    return EmailVerificationResult.InvalidCode(AppConstants.Limits.MaxOtpAttempts - attemptCount - 1);
                }

                // Clear OTP attempt info on successful verification
                await _cacheService.RemoveAsync(otpAttemptKey);

                // Generate verification token for registration completion
                var verificationToken = Guid.NewGuid().ToString();
                var verificationKey = CacheKeys.Authentication.EmailVerification(email, verificationToken);
                var verificationExpiresAt = DateTime.UtcNow.AddMinutes(10);

                await _cacheService.SetAsync(verificationKey,
                    new RegisterVerificationData(email, verifyResponse.User.Id, DateTime.UtcNow),
                    CacheKeys.Duration.EmailVerification);

                return EmailVerificationResult.CodeVerifiedSuccessfully(verificationToken, verificationExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email code for {Email}", email);
                return (EmailVerificationResult)EmailVerificationResult.Failed("Verification failed. Please try again.");
            }
        }

        public async Task<EmailVerificationResult> ResendVerificationCode(string email)
        {
            try
            {
                _logger.LogInformation("Resending verification code to {Email}", email);

                // Check if there's an existing OTP attempt
                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

                // Implement cooldown period (30 seconds between resend requests)
                if (otpAttemptInfo != null)
                {
                    var sentAt = otpAttemptInfo.SentAt;
                    //var sentAt = otpAttemptInfo.GetProperty("SentAt").GetDateTime();
                    var cooldownEnd = sentAt.AddSeconds(30);

                    if (DateTime.UtcNow < cooldownEnd)
                    {
                        var remainingSeconds = (int)(cooldownEnd - DateTime.UtcNow).TotalSeconds;
                        return (EmailVerificationResult)EmailVerificationResult.Failed($"Please wait {remainingSeconds} seconds before requesting a new code.");
                    }
                }

                // Send new verification code
                return await SendVerificationCode(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification code to {Email}", email);
                return (EmailVerificationResult)EmailVerificationResult.Failed("Failed to resend verification code. Please try again.");
            }
        }

        public async Task<bool> IsEmailAlreadyRegistered(string email)
        {
            try
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email is registered: {Email}", email);
                return false;
            }
        }

    }
}
