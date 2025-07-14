using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MomomiAPI.Data;
using MomomiAPI.Services.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using System.Security.Claims;
using static MomomiAPI.Models.Requests.AuthenticationRequests;
using User = MomomiAPI.Models.Entities.User;

namespace MomomiAPI.Services.Implementations
{
    // Helper class for OTP attempt tracking
    public class OtpAttemptInfo
    {
        public string Email { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class SupabaseAuthService : IAuthService
    {
        private readonly MomomiDbContext _context;
        private readonly Supabase.Client _supabaseClient;
        private readonly IJwtService _jwtService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<SupabaseAuthService> _logger;

        public SupabaseAuthService(
            MomomiDbContext context,
            Supabase.Client supabaseClient,
            ICacheService cacheService,
            ILogger<SupabaseAuthService> logger,
            IJwtService jwtService)
        {
            _context = context;
            _supabaseClient = supabaseClient;
            _cacheService = cacheService;
            _logger = logger;
            _jwtService = jwtService;
        }

        public async Task<OtpResult> SendOtpAsync(SendOtpRequest request)
        {
            try
            {
                _logger.LogInformation("Sending OTP to {Email}", request.Email);

                // Check rate limiting
                var rateLimitKey = $"otp_rate_limit_{request.Email}";
                var rateLimitCount = await _cacheService.GetAsync<int?>(rateLimitKey) ?? 0;

                if (rateLimitCount >= 3) // Max 3 OTP requests per hour
                {
                    return new OtpResult { Success = false, Error = "Rate limit exceeded. Please try again later.", RemainingAttempts = 0 };
                }

                // Send OTP via Supabase
                var otpResponse = await _supabaseClient.Auth.SignInWithOtp(
                    options: new SignInWithPasswordlessEmailOptions(request.Email)
                    {
                        EmailRedirectTo = null, // We handle verification in-app
                        Data = new Dictionary<string, object>
                        {
                            { "app_name", "Momomi" },
                            { "purpose", "login_verification" }
                        }
                    }
                );

                if (otpResponse == null)
                {
                    return new OtpResult
                    {
                        Success = false,
                        Error = "Failed to send OTP. Please try again later."
                    };
                }

                // Update rate limiting
                await _cacheService.SetAsync(rateLimitKey, rateLimitCount + 1, TimeSpan.FromHours(1));

                // Store OTP attempt info for validation
                var otpAttemptKey = $"otp_attempt_{request.Email}";
                var otpAttempInfo = new OtpAttemptInfo
                {
                    Email = request.Email,
                    AttemptCount = 0,
                    SentAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10) // OTP valid for 10 minutes
                };

                await _cacheService.SetAsync(otpAttemptKey, otpAttempInfo, TimeSpan.FromMinutes(10));

                _logger.LogInformation("OTP sent successfully to {Email}", request.Email);

                return new OtpResult
                {
                    Success = true,
                    Message = "OTP sent successfully. Please check your email.",
                    ExpiresAt = otpAttempInfo.ExpiresAt,
                    RemainingAttempts = 3 - otpAttempInfo.AttemptCount
                };
            }
            catch (GotrueException ex)
            {
                _logger.LogError(ex, "Supabase error sending OTP to {Email}: {Message}", request.Email, ex.Message);
                return new OtpResult
                {
                    Success = false,
                    Error = "Failed to send OTP. Please verify your email address."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending OTP to {Email}", request.Email);
                return new OtpResult
                {
                    Success = false,
                    Error = "An unexpected error occurred. Please try again."
                };
            }
        }

        public async Task<EmailVerificationResult> VerifyEmailOtpAsync(VerifyOtpRequest request)
        {
            try
            {
                _logger.LogInformation("Verifying email OTP for {Email}", request.Email);

                // Check OTP attempt info
                var otpAttemptKey = $"otp_attempt_{request.Email}";
                var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

                if (otpAttemptInfo == null || otpAttemptInfo.ExpiresAt < DateTime.UtcNow)
                {
                    return new EmailVerificationResult { Success = false, Error = "OTP expired or not found." };
                }

                // Check if OTP is valid
                if (otpAttemptInfo.AttemptCount >= 3)
                {
                    return new EmailVerificationResult { Success = false, Error = "Too many failed attempts. Please request a new OTP." };
                }

                // Verify the OTP using Supabase Auth (without creating session)
                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(request.Email, request.Otp, type: Constants.EmailOtpType.Email);

                if (verifyResponse == null || verifyResponse?.User == null)
                {
                    // Increment attempt count if OTP is invalid
                    otpAttemptInfo.AttemptCount++;
                    await _cacheService.SetAsync(otpAttemptKey, otpAttemptInfo,
                                                TimeSpan.FromMinutes((int)(otpAttemptInfo.ExpiresAt - DateTime.UtcNow).TotalMinutes));

                    return new EmailVerificationResult
                    {
                        Success = false,
                        Error = $"Invalid OTP. {3 - otpAttemptInfo.AttemptCount} attempts remaining."
                    };
                }

                // Clear OTP attempt info on successful verification
                await _cacheService.RemoveAsync(otpAttemptKey);

                // Generate a temporary verification token for registration completion
                var verificationToken = Guid.NewGuid().ToString();
                var verificationKey = $"email_verified_{request.Email}_{verificationToken}";

                // Store verification status for 10 minutes
                await _cacheService.SetAsync(verificationKey, new
                {
                    Email = request.Email,
                    SupabaseUserId = verifyResponse.User.Id,
                    VerifiedAt = DateTime.UtcNow
                }, TimeSpan.FromMinutes(10));

                _logger.LogInformation("Email OTP verified successfully for {Email}", request.Email);

                return new EmailVerificationResult
                {
                    Success = true,
                    Message = "Email verified successfully. You can now complete your registration.",
                    VerificationToken = verificationToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                };
            }
            catch (GotrueException ex)
            {
                _logger.LogError(ex, "Supabase error verifying email OTP for {Email}: {Message}", request.Email, ex.Message);
                return new EmailVerificationResult
                {
                    Success = false,
                    Error = "Invalid OTP or verification failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error verifying email OTP for {Email}", request.Email);
                return new EmailVerificationResult
                {
                    Success = false,
                    Error = "An unexpected error occurred during verification."
                };
            }
        }

        public async Task<AuthResult> VerifyOtpAndLoginAsync(LoginWithOtpRequest request)
        {
            try
            {
                _logger.LogInformation("Verifying OTP for {Email}", request.Email);

                // Check OTP attempt info
                var otpAttemptKey = $"otp_attempt_{request.Email}";
                var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

                if (otpAttemptInfo == null || otpAttemptInfo.ExpiresAt < DateTime.UtcNow)
                {
                    return new AuthResult { Success = false, Error = "OTP expired or not found." };
                }

                // Check if OTP is valid
                if (otpAttemptInfo.AttemptCount >= 3)
                {
                    return new AuthResult { Success = false, Error = "Too many failed attempts. Please request a new OTP." };
                }

                // Verify the OTP using Supabase Auth
                var verifyResponse = await _supabaseClient.Auth.VerifyOTP(request.Email, request.Otp, type: Constants.EmailOtpType.Email);
                if (verifyResponse == null || verifyResponse?.User == null)
                {
                    // Increment attempt count if OTP is invalid
                    otpAttemptInfo.AttemptCount++;
                    await _cacheService.SetAsync(otpAttemptKey, otpAttemptInfo,
                                                TimeSpan.FromMinutes((int)(otpAttemptInfo.ExpiresAt - DateTime.UtcNow).TotalMinutes));

                    return new AuthResult
                    {
                        Success = false,
                        Error = $"Invalid OTP. {3 - otpAttemptInfo.AttemptCount} attempts remaining."
                    };
                }

                // Clear OTP attempt info on successful login
                await _cacheService.RemoveAsync(otpAttemptKey);

                // Check if user exists in the database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.SupabaseUid == Guid.Parse(verifyResponse.User.Id!));

                if (user == null)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "User not found. Please register first."
                    };
                }

                // Generate YOUR custom tokens instead of using Supabase tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Store refresh token
                await _jwtService.StoreRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

                // Update last active time
                user.LastActive = DateTime.UtcNow;
                await _context.SaveChangesAsync();


                _logger.LogInformation("User logged in successfully: {Email}", request.Email);

                return new AuthResult
                {
                    Success = true,
                    User = user,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                };
            }
            catch (GotrueException ex)
            {
                _logger.LogError(ex, "Supabase error verifying OTP for {Email}: {Message}", request.Email, ex.Message);
                return new AuthResult
                {
                    Success = false,
                    Error = "Invalid OTP or verification failed."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error verifying OTP for {Email}", request.Email);
                return new AuthResult
                {
                    Success = false,
                    Error = "An unexpected error occurred during verification."
                };
            }
        }
        public async Task<AuthResult> CompleteRegistrationAsync(CompleteRegistrationRequest request)
        {
            try
            {
                _logger.LogInformation("Registering user with OTP: {Email}", request.Email);

                // Check if email is already registered
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (existingUser != null)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Email already registered. Please login instead."
                    };
                }

                // Verify the verification token
                var verificationKey = $"email_verified_{request.Email}_{request.VerificationToken}";
                var verificationData = await _cacheService.GetAsync<dynamic>(verificationKey);

                if (verificationData == null)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Invalid or expired verification token. Please verify your email again."
                    };
                }

                // Clear verification token (single use)
                await _cacheService.RemoveAsync(verificationKey);

                // Extract Supabase user ID from verification data
                var supabaseUserId = verificationData.GetProperty("SupabaseUserId").GetString();
                if (string.IsNullOrEmpty(supabaseUserId))
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Invalid verification data. Please try again."
                    };
                }

                // Create user in our database
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    SupabaseUid = Guid.Parse(supabaseUserId),
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    DateOfBirth = request.DateOfBirth.ToUniversalTime(),
                    Gender = request.Gender,
                    InterestedIn = request.InterestedIn,
                    PhoneNumber = request.PhoneNumber,

                    // Optional fields provided during registration
                    Bio = request.Bio,
                    Hometown = request.Hometown,
                    Heritage = request.Heritage,
                    Religion = request.Religion,
                    LanguagesSpoken = request.LanguagesSpoken,

                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Generate our custom tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Store refresh token
                await _jwtService.StoreRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

                _logger.LogInformation("User registration completed successfully: {Email}", request.Email);

                return new AuthResult
                {
                    Success = true,
                    User = user,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                };

            }
            catch (GotrueException ex)
            {
                _logger.LogError(ex, "Supabase error during registration for {Email}: {Message}", request.Email, ex.Message);
                return new AuthResult
                {
                    Success = false,
                    Error = "Registration failed. Please verify your OTP and try again."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for {Email}", request.Email);
                return new AuthResult
                {
                    Success = false,
                    Error = "An unexpected error occurred during registration."
                };
            }
        }

        public async Task<OtpResult> ResendOtpAsync(ResendOtpRequest request)
        {
            try
            {
                _logger.LogInformation("Resending OTP to email: {Email}", request.Email);

                // Check if there's an existing OTP attempt
                var otpAttemptKey = $"otp_attempt_{request.Email}";
                var otpAttemptInfo = await _cacheService.GetAsync<OtpAttemptInfo>(otpAttemptKey);

                // Implement cooldown period (30 seconds between resend requests)
                if (otpAttemptInfo != null && DateTime.UtcNow < otpAttemptInfo.SentAt.AddSeconds(30))
                {
                    var remainingSeconds = (int)(otpAttemptInfo.SentAt.AddSeconds(30) - DateTime.UtcNow).TotalSeconds;
                    return new OtpResult
                    {
                        Success = false,
                        Error = $"Please wait {remainingSeconds} seconds before requesting a new OTP."
                    };
                }

                // Send new OTP
                var sendOtpRequest = new SendOtpRequest { Email = request.Email };
                return await SendOtpAsync(sendOtpRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending OTP to {Email}", request.Email);
                return new OtpResult
                {
                    Success = false,
                    Error = "Failed to resend OTP. Please try again."
                };
            }
        }

        public async Task<User?> GetUserFromTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogDebug("Access token is null or empty");
                    return null;
                }

                // Clean the token (remove 'Bearer ' if present)
                var cleanToken = accessToken.Replace("Bearer ", "").Trim();

                // Validate the token using our JWT service
                var principal = _jwtService.ValidateToken(cleanToken, validateLifetime: true);

                if (principal == null)
                {
                    _logger.LogDebug("Token validation failed - invalid token");
                    return null;
                }

                // Extract user ID from the validated token claims
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                                 principal.FindFirst("user_id")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Token missing or contains invalid user ID claim: {UserIdClaim}", userIdClaim);
                    return null;
                }

                // Check if token is blacklisted (optional security check)
                var jtiClaim = principal.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jtiClaim))
                {
                    var isBlacklisted = await _jwtService.IsTokenBlacklistedAsync(jtiClaim);
                    if (isBlacklisted)
                    {
                        _logger.LogWarning("Token is blacklisted: {Jti}", jtiClaim);
                        return null;
                    }
                }

                // Get user from database
                var user = await _context.Users
                    .Include(u => u.Photos)
                    .Include(u => u.Preferences)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("User not found or inactive for ID: {UserId}", userId);
                    return null;
                }

                _logger.LogDebug("Successfully retrieved user from token: {UserId}", user.Id);
                return user;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogDebug("Token has expired");
                return null;
            }
            catch (SecurityTokenValidationException ex)
            {
                _logger.LogDebug("Token validation failed: {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting user from token");
                return null;
            }
        }
        public async Task<bool> LogoutAsync(string accessToken)
        {
            try
            {
                //var tokenInfo = _jwtService.GetTokenInfo(accessToken);
                //if (tokenInfo.HasValue)
                //{
                //    // Blacklist the current access token
                //    await _jwtService.BlacklistTokenAsync(tokenInfo.Value.jti, tokenInfo.Value.expiry);
                //}

                // Get user from token and revoke refresh token
                var principal = _jwtService.ValidateToken(accessToken, false); // Don't validate lifetime for logout
                var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    await _jwtService.RevokeRefreshTokenAsync(userId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Refresh token is required"
                    };
                }

                // Get user ID directly using optimized lookup
                var userId = await _jwtService.GetUserIdByRefreshTokenAsync(refreshToken);
                if (!userId.HasValue)
                {
                    return new AuthResult { Success = false, Error = "Invalid refresh token" };
                }

                var user = await _context.Users
                    .Where(u => u.Id == userId.Value && u.IsActive)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return new AuthResult { Success = false, Error = "User not found or inactive" };
                }

                // Generate new tokens
                var newAccessToken = _jwtService.GenerateAccessToken(user);
                var newRefreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Store new refresh token (this will revoke the old one)
                await _jwtService.RevokeRefreshTokenAsync(user.Id);
                await _jwtService.StoreRefreshTokenAsync(user.Id, newRefreshToken, refreshExpiry);

                // Update last active
                user.LastActive = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AuthResult
                {
                    Success = true,
                    User = user,
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return new AuthResult { Success = false, Error = "Token refresh failed" };
            }
        }

        public async Task<bool> IsEmailRegisteredAsync(string email)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
                return user != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email is registered: {Email}", email);
                return false;
            }
        }

        public async Task<bool> RevokeUserSessionsAsync(Guid userId)
        {
            try
            {
                await _jwtService.RevokeAllUserTokensAsync(userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking user sessions");
                return false;
            }
        }
    }
}
