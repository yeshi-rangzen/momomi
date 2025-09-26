using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using NetTopologySuite.Geometries;
using Supabase.Gotrue;
using System.Text.Json.Serialization;
using static MomomiAPI.Models.Requests.AuthenticationRequests;
using static Supabase.Gotrue.Constants;
using User = MomomiAPI.Models.Entities.User;

namespace MomomiAPI.Services.Implementations
{
    public class AuthService : IAuthService
    {

        private readonly Supabase.Client _supabaseClient;
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthService> _logger;
        private readonly IUserService _userService;

        public AuthService(
           Supabase.Client supabaseClient,
           MomomiDbContext dbContext,
           ICacheService cacheService,
           IJwtService jwtService,
           ILogger<AuthService> logger,
           IUserService userService)
        {
            _supabaseClient = supabaseClient;
            _dbContext = dbContext;
            _cacheService = cacheService;
            _jwtService = jwtService;
            _logger = logger;
            _userService = userService;
        }

        public async Task<RegistrationResult> RegisterNewUser(RegistrationRequest request)
        {
            try
            {
                _logger.LogInformation("Starting registration for {Email}", request.Email);

                var today = DateTime.UtcNow.Date;
                var minBirthDate = today.AddYears(-AppConstants.Limits.MinAge);

                if (request.DateOfBirth > minBirthDate)
                {
                    return RegistrationResult.UnderageUser();
                }

                var existingUserExists = await _dbContext.Users
                    .AnyAsync(u => u.Email == request.Email);

                if (existingUserExists)
                {
                    return RegistrationResult.EmailAlreadyRegistered();
                }

                // Verify verification token
                var varificationKey = CacheKeys.Authentication.EmailVerificationToken(request.Email, request.VerificationToken);
                var verificationData = await _cacheService.GetAsync<RegisterVerificationData>(varificationKey);

                if (verificationData == null || verificationData.Email != request.Email)
                {
                    return RegistrationResult.InvalidVerificationToken();
                }

                var user = CreateNewUser(request, verificationData.SupabaseUserId);
                _dbContext.Users.Add(user);

                await _dbContext.SaveChangesAsync();

                // Token generation
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();

                _ = _cacheService.RemoveAsync(varificationKey);

                // Store refresh token to Cache
                _ = _jwtService.CacheRefreshTokenAsync(user.Id, refreshToken);

                _logger.LogInformation("User registered successfully: {Email}", request.Email);

                var userDto = UserMapper.UserToDTO(user);
                return RegistrationResult.Successful(userDto, accessToken, refreshToken, DateTime.UtcNow.AddHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", request.Email);
                return RegistrationResult.Error("An error occurred during registration.");
            }
        }

        public async Task<LoginResult> LoginWithEmailCode(string email, string code)
        {
            try
            {
                _logger.LogInformation("Processing login for {Email}", email);

                var otpValidationResult = await ValidateOtpAttempt(email);
                if (!otpValidationResult.IsValid)
                {
                    return LoginResult.InvalidCredentials();
                }
                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(email, code, EmailOtpType.Email);
                if (verifyResponse?.User == null)
                {
                    // Update attempt count on failed verification
                    _ = UpdateOtpAttemptCount(email, otpValidationResult.AttemptData!);
                    return LoginResult.InvalidCredentials();
                }

                var supabaseUid = Guid.Parse(verifyResponse.User.Id!);
                var userResult = await _userService.GetUserProfileWithSupabaseIdAsync(supabaseUid);
                var userDto = userResult.Data?.User;

                if (!userResult.Success || userDto == null)
                {
                    return LoginResult.UserNotFound();
                }

                // Token generation
                var accessToken = _jwtService.GenerateAccessToken(userDto);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var tokenExpiresAt = DateTime.UtcNow.AddHours(1);

                // Store refresh token
                _ = _jwtService.CacheRefreshTokenAsync(userDto.Id, refreshToken);

                _logger.LogInformation("User logged in successfully: {Email}", email);

                return LoginResult.Successful(userDto, accessToken, refreshToken, tokenExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", email);
                return LoginResult.Error("Login failed. Please try again.");
            }
        }

        public async Task<EmailVerificationResult> SendOTPCode(string email)
        {
            try
            {
                _logger.LogInformation("Sending OTP code to {Email}", email);
                var rateLimitKey = CacheKeys.Authentication.OtpRateLimit(email);

                // Start both tasks in parallel
                var getRateLimitTask = _cacheService.GetAsync<int?>(rateLimitKey);
                var emailExistsTask = IsEmailRegistered(email);

                // Wait for both to complete
                await Task.WhenAll(getRateLimitTask, emailExistsTask);

                // Get results
                var rateLimitCount = getRateLimitTask.Result ?? 0;
                var isEmailRegistered = emailExistsTask.Result;

                // Early return if rate limited
                if (rateLimitCount >= AppConstants.Limits.MaxOtpRequestsPerHour)
                {
                    return EmailVerificationResult.RateLimitExceeded(0, email);
                }

                var otpResponse = await SendOtpToSupabase(email);
                if (otpResponse == null)
                {
                    return EmailVerificationResult.Error("Failed to send verification code. Please try again.", email);
                }

                var now = DateTime.UtcNow;
                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var otpAttemptData = new OtpAttemptData(email, 0, DateTime.UtcNow, now.AddMinutes(10));

                var caches = new Dictionary<string, object?>
                {
                    { rateLimitKey, rateLimitCount + 1 },
                    { otpAttemptKey, otpAttemptData},
                };

                var cacheExpirations = new Dictionary<string, TimeSpan>
                {
                    { rateLimitKey, CacheKeys.Duration.OtpRateLimit },
                    { otpAttemptKey, CacheKeys.Duration.OtpAttempt },
                };

                _ = _cacheService.SetManyAsync(caches, cacheExpirations);

                _logger.LogInformation("OTP code sent successfully to {Email}", email);

                return EmailVerificationResult.CodeSentSuccessfully(
                   null!, // verificationToken is not used here
                   otpAttemptData.ExpiresAt,
                   AppConstants.Limits.MaxOtpAttempts - otpAttemptData.AttemptCount,
                   isEmailRegistered, // Frontend will decide for login flow based on this flag
                   email // Pass email for frontend to use in verification
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification code to {Email}", email);
                return EmailVerificationResult.Error("Failed to send verification code. Please try again.", email);
            }
        }

        public async Task<EmailVerificationResult> VerifyOTPCode(string email, string code)
        {
            try
            {
                _logger.LogInformation("Verifying otp code for {Email}", email);

                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var validationResult = await ValidateOtpAttempt(email);
                if (!validationResult.IsValid)
                {
                    return EmailVerificationResult.OtpCodeExpired(email);
                }

                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(email, code, EmailOtpType.Email);

                if (verifyResponse?.User == null)
                {
                    _ = UpdateOtpAttemptCount(otpAttemptKey, validationResult.AttemptData!);

                    var remainingAttempts = AppConstants.Limits.MaxOtpAttempts - validationResult.AttemptData!.AttemptCount - 1;
                    return EmailVerificationResult.InvalidOtpCode(remainingAttempts, email);
                }

                var verificationToken = Guid.NewGuid().ToString();
                var verificationKey = CacheKeys.Authentication.EmailVerificationToken(email, verificationToken);
                var verificationData = new RegisterVerificationData(email, verifyResponse.User.Id!, DateTime.UtcNow);

                _ = _cacheService.SetAsync(verificationKey, verificationData, CacheKeys.Duration.EmailVerificationCode);
                _ = _cacheService.RemoveAsync(otpAttemptKey);

                _logger.LogInformation("Email verification successful for {Email}", email);

                return EmailVerificationResult.CodeVerifiedSuccessfully(
                    verificationToken,
                    DateTime.UtcNow.AddMinutes(30),
                    false, email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP code for {Email}", email);
                return EmailVerificationResult.Error("Failed to verify OTP code. Please try again.", email);
            }
        }

        public async Task<EmailVerificationResult> ResendOTPCode(string email)
        {
            try
            {
                _logger.LogInformation("Resending OTP code to {Email}", email);

                // Check cooldown efficiently
                var cooldownResult = await CheckResendCooldown(email);
                if (!cooldownResult.CanResend)
                {
                    return EmailVerificationResult.ValidationError(cooldownResult.ErrorMessage!, email);
                }

                // ✅ Reuse existing send logic
                return await SendOTPCode(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification code to {Email}", email);
                return EmailVerificationResult.Error("Failed to resend verification code. Please try again.", email);
            }
        }

        #region Private Helper Methods
        private async Task<PasswordlessSignInState?> SendOtpToSupabase(string email)
        {
            try
            {
                return await _supabaseClient.Auth.SignInWithOtp(
                    options: new SignInWithPasswordlessEmailOptions(email)
                    {
                        Data = new Dictionary<string, object>
                        {
                            {"app_name", "mimori"},
                            {"purpose", "email_verification"}
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP via Supabase for {Email}", email);
                return null;
            }
        }

        private async Task<bool> IsEmailRegistered(string email)
        {
            try
            {
                return await _dbContext.Users
                    .Where(u => u.Email == email && u.IsActive)
                    .AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email is registered: {Email}", email);
                return false;
            }
        }

        private static User CreateNewUser(RegistrationRequest request, string supabaseUid)
        {
            var now = DateTime.UtcNow;

            return new User
            {
                SupabaseUid = Guid.Parse(supabaseUid),
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth.ToUniversalTime(),
                Gender = request.Gender,
                InterestedIn = request.InterestedIn,
                PhoneNumber = request.PhoneNumber,
                Bio = request.Bio,
                Hometown = request.Hometown,
                Heritage = request.Heritage,
                Religion = request.Religion,
                LanguagesSpoken = request.LanguagesSpoken,
                Location = new Point(request.Latitude, request.Longitude)
                {
                    SRID = 4326 // WGS84 standard for geospatial
                },
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Neighbourhood = request.Neighbourhood,

                // Default values
                EnableGlobalDiscovery = AppConstants.DefaultValues.DefaultGlobalDiscovery,
                IsDiscoverable = AppConstants.DefaultValues.DefaultIsDiscoverable,
                IsGloballyDiscoverable = AppConstants.DefaultValues.DefaultIsGloballyDiscoverable,
                NotificationsEnabled = AppConstants.DefaultValues.DefaultNotificationsEnabled,
                MaxDistanceKm = AppConstants.DefaultValues.DefaultMaxDistance,
                MinAge = AppConstants.DefaultValues.DefaultMinAge,
                MaxAge = AppConstants.DefaultValues.DefaultMaxAge,

                // Status and timing
                CreatedAt = now,
                LastActive = now,
                IsActive = true,
                IsOnboarding = true,

                // Related entities
                Preferences = new UserPreference
                {
                    PreferredHeritage = request.Heritage,
                    LanguagePreference = [],
                    PreferredReligions = []
                },
                Subscription = new UserSubscription
                {
                    SubscriptionType = SubscriptionType.Free,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                UsageLimit = new UserUsageLimit
                {
                    LastResetDate = now.Date,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };
        }

        private async Task<OtpValidationResult> ValidateOtpAttempt(string email)
        {
            var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
            var otpAttemptData = await _cacheService.GetAsync<OtpAttemptData>(otpAttemptKey);
            if (otpAttemptData == null)
            {
                return OtpValidationResult.Invalid();
            }

            var now = DateTime.UtcNow;
            if (otpAttemptData.ExpiresAt < now || otpAttemptData.AttemptCount >= AppConstants.Limits.MaxOtpAttempts)
            {
                return OtpValidationResult.Invalid();
            }

            return OtpValidationResult.Valid(otpAttemptData);
        }

        private async Task UpdateOtpAttemptCount(string email, OtpAttemptData attemptData)
        {
            var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
            var updatedAttemptData = new OtpAttemptData(
                email, attemptData.AttemptCount + 1, attemptData.SentAt, attemptData.ExpiresAt);

            var remainingTime = attemptData.ExpiresAt - DateTime.UtcNow;
            if (remainingTime > TimeSpan.Zero)
            {
                await _cacheService.SetAsync(otpAttemptKey, updatedAttemptData, remainingTime);
            }
        }

        /// Updates only the LastActive field to minimize database load
        private async Task UpdateUserLastActive(Guid userId)
        {
            await _dbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.LastActive, DateTime.UtcNow)
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
        }

        private async Task<ResendCooldownResult> CheckResendCooldown(string email)
        {
            var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
            var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

            if (otpAttemptInfo == null)
            {
                return ResendCooldownResult.CanResendOTP();
            }

            var cooldownEnd = otpAttemptInfo.SentAt.Add(CacheKeys.Duration.ResendCooldown);
            var now = DateTime.UtcNow;

            if (now < cooldownEnd)
            {
                var remainingSeconds = (int)(cooldownEnd - now).TotalSeconds;
                return ResendCooldownResult.InCooldown($"Please wait {remainingSeconds} seconds before requesting a new code.");
            }

            return ResendCooldownResult.CanResendOTP();
        }
        #endregion

        #region Helper classes
        public class RegisterVerificationData
        {
            public string Email { get; set; }
            public string SupabaseUserId { get; set; }
            public DateTime VerifiedAt { get; set; }

            public RegisterVerificationData(string email, string supabaseUserId, DateTime verifiedAt)
            {
                Email = email;
                SupabaseUserId = supabaseUserId;
                VerifiedAt = verifiedAt;
            }
        }
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
        public class OtpAttemptData
        {
            public string Email { get; set; }
            public int AttemptCount { get; set; }
            public DateTime SentAt { get; set; } = DateTime.UtcNow;
            public DateTime ExpiresAt { get; set; }

            [JsonConstructor]
            public OtpAttemptData(string email, int attemptCount, DateTime sentAt, DateTime expiresAt)
            {
                Email = email;
                AttemptCount = attemptCount;
                SentAt = sentAt;
                ExpiresAt = expiresAt;
            }
        }
        #endregion
    }
}
