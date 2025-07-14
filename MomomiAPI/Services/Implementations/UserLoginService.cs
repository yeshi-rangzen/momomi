using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Services.Interfaces;
using Supabase.Gotrue;

namespace MomomiAPI.Services.Implementations
{
    public class UserLoginService : IUserLoginService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly IJwtService _jwtService;
        private readonly ILogger<UserLoginService> _logger;

        public UserLoginService(
            Supabase.Client supabaseClient,
            MomomiDbContext dbContext,
            ICacheService cacheService,
            IJwtService jwtService,
            ILogger<UserLoginService> logger)
        {
            _supabaseClient = supabaseClient;
            _dbContext = dbContext;
            _cacheService = cacheService;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<LoginResult> LoginWithEmailCode(string email, string code)
        {
            try
            {
                _logger.LogInformation("Processing login for {Email}", email);

                // Check OTP attempt info
                var otpAttemptKey = CacheKeys.Authentication.OtpAttempt(email);
                var otpAttemptInfo = await _cacheService.GetAsync<dynamic>(otpAttemptKey);

                if (otpAttemptInfo == null)
                {
                    return LoginResult.InvalidCredentials();
                }

                var expiresAt = otpAttemptInfo.GetProperty("ExpiresAt").GetDateTime();
                var attemptCount = otpAttemptInfo.GetProperty("AttemptCount").GetInt32();

                if (expiresAt < DateTime.UtcNow)
                {
                    return LoginResult.InvalidCredentials();
                }

                if (attemptCount >= AppConstants.Limits.MaxOtpAttempts)
                {
                    return LoginResult.InvalidCredentials();
                }

                // Verify the OTP using Supabase Auth
                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(email, code, Constants.EmailOtpType.Email);

                if (verifyResponse?.User == null)
                {
                    // Increment attempt count
                    var updatedAttemptInfo = new
                    {
                        Email = email,
                        AttemptCount = attemptCount + 1,
                        SentAt = otpAttemptInfo.GetProperty("SentAt").GetDateTime(),
                        ExpiresAt = expiresAt
                    };

                    await _cacheService.SetAsync(otpAttemptKey, updatedAttemptInfo,
                        TimeSpan.FromMinutes((expiresAt - DateTime.UtcNow).TotalMinutes));

                    return LoginResult.InvalidCredentials();
                }

                // Clear OTP attempt info on successful verification
                await _cacheService.RemoveAsync(otpAttemptKey);

                // Check if user exists in our database
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.SupabaseUid == Guid.Parse(verifyResponse.User.Id!));

                if (user == null)
                {
                    return LoginResult.UserNotFound();
                }

                if (!user.IsActive)
                {
                    return LoginResult.AccountInactive();
                }

                // Generate our custom tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Store refresh token
                await _jwtService.StoreRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

                // Update last active time
                user.LastActive = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User logged in successfully: {Email}", email);

                return LoginResult.Success(user, accessToken, refreshToken, DateTime.UtcNow.AddHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", email);
                return (LoginResult)LoginResult.Failed("Login failed. Please try again.");
            }
        }
    }
}