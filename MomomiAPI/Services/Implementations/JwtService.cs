using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using User = MomomiAPI.Models.Entities.User;

namespace MomomiAPI.Services.Implementations
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ICacheService _cacheService;
        private readonly ILogger<JwtService> _logger;
        private readonly MomomiDbContext _dbContext;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly JwtSecurityTokenHandler _tokenHandler;
        private readonly SigningCredentials _signingCredentials;
        private readonly SymmetricSecurityKey _signingKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenExpiryMinutes;

        public JwtService(IConfiguration configuration, ICacheService cacheService, ILogger<JwtService> logger, MomomiDbContext dbContext)
        {
            _configuration = configuration;
            _cacheService = cacheService;
            _logger = logger;
            _dbContext = dbContext;

            // Initialize expensive objects once
            var secretKey = _configuration["JWT:SecretKey"] ?? throw new ArgumentNullException("JWT:Secret configuration is missing.");
            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            _issuer = _configuration["JWT:Issuer"] ?? throw new ArgumentNullException("JWT:Issuer configuration is missing.");
            _audience = _configuration["JWT:Audience"] ?? throw new ArgumentNullException("JWT:Audience configuration is missing.");
            _accessTokenExpiryMinutes = int.Parse(_configuration["JWT:AccessTokenExpiryMinutes"] ?? "60");

            _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature);
            _tokenHandler = new JwtSecurityTokenHandler();

            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = _signingKey,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        }
        #region Completed Methods
        public string GenerateAccessToken(User user)
        {
            try
            {
                var jwtId = Guid.NewGuid().ToString();
                var issuedAt = DateTime.UtcNow;
                var expires = issuedAt.AddMinutes(_accessTokenExpiryMinutes);

                var claims = CreateUserClaims(user, jwtId, issuedAt);

                var tokenDisciptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expires,
                    Issuer = _issuer,
                    Audience = _audience,
                    SigningCredentials = _signingCredentials
                };

                var token = _tokenHandler.CreateToken(tokenDisciptor);
                var tokenString = _tokenHandler.WriteToken(token);
                _logger.LogDebug("Generated access token for user {UserId} with JTI {Jti}", user.Id, jwtId);

                return tokenString;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
                throw;
            }
        }

        public string GenerateAccessToken(UserDTO user)
        {
            try
            {
                var jwtId = Guid.NewGuid().ToString();
                var issuedAt = DateTime.UtcNow;
                var expires = issuedAt.AddMinutes(_accessTokenExpiryMinutes);

                var claims = CreateUserClaims(user, jwtId, issuedAt);

                var tokenDisciptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expires,
                    Issuer = _issuer,
                    Audience = _audience,
                    SigningCredentials = _signingCredentials
                };

                var token = _tokenHandler.CreateToken(tokenDisciptor);
                var tokenString = _tokenHandler.WriteToken(token);
                _logger.LogDebug("Generated access token for user {UserId} with JTI {Jti}", user.Id, jwtId);

                return tokenString;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
                throw;
            }
        }


        public string GenerateRefreshToken()
        {
            try
            {
                var randomBytes = new byte[64];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomBytes);

                // Add timestamp to ensure uniqueness
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var combinedBytes = Encoding.UTF8.GetBytes(timestamp.ToString()).Concat(randomBytes).ToArray();

                // URL-safe tokens after replacing promblematic characters
                return Convert.ToBase64String(combinedBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                throw;
            }
        }

        public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
        {
            try
            {
                // Clone only when needed
                var validationParameters = validateLifetime
                    ? _tokenValidationParameters
                    : CreateNonLifetimeValidationParameters();

                var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (!IsValidJwtToken(validatedToken))
                {
                    _logger.LogWarning("Token validation failed: Invalid JWT structure");
                    return null;
                }

                return principal;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogDebug("Token validation failed: Token expired");
                return null;
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "Token validation failed: {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return null;
            }
        }
        #endregion

        #region Token Revocation & Security
        // Blacklist tokens for immediate security (account compromise, logout)
        public async Task BlacklistTokenAsync(string jti, DateTime expiry)
        {
            try
            {
                var blacklistKey = CacheKeys.Authentication.BlacklistedToken(jti);
                var ttl = expiry > DateTime.UtcNow ? expiry - DateTime.UtcNow : TimeSpan.FromMinutes(1);

                await _cacheService.SetAsync(blacklistKey, true, ttl);
                _logger.LogInformation("Blacklisted token with JTI {Jti} until {Expiry}", jti, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blacklisting token: {Jti}", jti);
                throw;
            }
        }

        public async Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            try
            {
                var blacklistKey = CacheKeys.Authentication.BlacklistedToken(jti);
                var isBlacklisted = await _cacheService.GetAsync<bool?>(blacklistKey);
                return isBlacklisted ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if token is blacklisted: {Jti}", jti);
                return false;
            }
        }

        // Global revocation for security incidents (data breach, account takeover)
        // Will revoke all tokens that were issued before the revocation timestamp
        public async Task RevokeAllUserTokensAsync(Guid userId)
        {
            try
            {
                var revocationKey = CacheKeys.Authentication.UserTokenRevocation(userId);
                var revocationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var revokeUserTokensTask = _cacheService.SetAsync(revocationKey, revocationTimestamp, CacheKeys.Duration.UserTokenRevocation);

                await Task.WhenAll(
                    revokeUserTokensTask,
                    RevokeRefreshTokenAsync(userId)
                    );
                _logger.LogWarning("Revoked all tokens for user {UserId} - SECURITY ACTION", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
                throw;
            }
        }

        // Checks if user tokens are globally revoked - meaning user is blocked for security reasons for 7 days
        public async Task<bool> AreUserTokensRevokedAsync(Guid userId, long tokenIssuedAt)
        {
            try
            {
                var revocationKey = CacheKeys.Authentication.UserTokenRevocation(userId);
                var revocationTimestamp = await _cacheService.GetAsync<long?>(revocationKey);

                return revocationTimestamp.HasValue && tokenIssuedAt < revocationTimestamp.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user token revocation for {UserId}", userId);
                return false; // Fail open
            }
        }
        #endregion

        #region Refresh Token Management
        public async Task<Guid?> GetUserIdFromRefreshTokenAsync(string refreshToken)
        {
            try
            {
                var tokenToUserKey = CacheKeys.Authentication.TokenToUser(refreshToken);
                return await _cacheService.GetAsync<Guid?>(tokenToUserKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving User Id for refresh token");
                return null;
            }
        }

        public async Task CacheRefreshTokenAsync(Guid userId, string refreshToken)
        {
            try
            {
                var refreshTokenKey = CacheKeys.Authentication.RefreshToken(userId);
                var tokenToUserKey = CacheKeys.Authentication.TokenToUser(refreshToken);
                var ttl = CacheKeys.Duration.RefreshToken;

                var caches = new Dictionary<string, object>
                {
                    { refreshTokenKey, refreshToken },
                    { tokenToUserKey , userId.ToString() }
                };

                var cacheExpiries = new Dictionary<string, TimeSpan>
                {
                    { refreshTokenKey, ttl },
                    { tokenToUserKey , ttl }
                };

                await _cacheService.SetManyAsync(caches, cacheExpiries);

                _logger.LogDebug("Cached refresh token for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing refresh token for user {UserId}", userId);
                throw;
            }
        }

        public async Task RevokeRefreshTokenAsync(Guid userId)
        {
            try
            {
                var refreshTokenKey = CacheKeys.Authentication.RefreshToken(userId);
                var currentRefreshToken = await _cacheService.GetAsync<string>(refreshTokenKey);

                // Parallel removal operations
                var removalTasks = new List<Task> { _cacheService.RemoveAsync(refreshTokenKey) };

                if (!string.IsNullOrEmpty(currentRefreshToken))
                {
                    var tokenToUserKey = CacheKeys.Authentication.TokenToUser(currentRefreshToken);
                    removalTasks.Add(_cacheService.RemoveAsync(tokenToUserKey));
                }

                await Task.WhenAll(removalTasks);

                _logger.LogDebug("Revoked refresh token for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token for user {UserId}", userId);
                throw;
            }
        }
        #endregion

        #region Token Refresh Functionality
        public async Task<RefreshTokenResult> RefreshUserTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogDebug("Processing token refresh request");

                // Validate refresh token format
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return RefreshTokenResult.InvalidToken("Refresh token is required");
                }

                var userId = await GetUserIdFromRefreshTokenAsync(refreshToken);
                if (userId == null)
                {
                    _logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
                    return RefreshTokenResult.InvalidToken("Invalid or expired refresh token");
                }

                // Check if user tokens are globally revoked
                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (await AreUserTokensRevokedAsync(userId.Value, currentTimestamp))
                {
                    _logger.LogWarning("Token refresh failed: All tokens revoked for user {UserId}", userId);
                    return RefreshTokenResult.TokensRevoked("All user tokens have been revoked");
                }

                var user = await GetUserForTokenRefreshAsync(userId.Value);

                if (user == null)
                {
                    _logger.LogWarning("Token refresh failed: User {UserId} not found or inactive", userId);
                    return RefreshTokenResult.UserNotFound("User not found or account is inactive");
                }

                // Generate new tokens
                var newAccessToken = GenerateAccessToken(user);
                var newRefreshToken = GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Atomic token rotation (revoke old, cache new)
                await RotateRefreshTokenAsync(userId.Value, refreshToken, newRefreshToken);

                _logger.LogInformation("Token refresh successful for user {UserId}", userId);

                return RefreshTokenResult.RefreshSuccess(
                    newAccessToken,
                    newRefreshToken,
                    DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes)
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return RefreshTokenResult.Error("Token refresh failed. Please login again.");
            }
        }

        /// Atomically rotates refresh token (security best practice)
        private async Task RotateRefreshTokenAsync(Guid userId, string oldRefreshToken, string newRefreshToken)
        {
            try
            {
                // ATOMIC OPERATION: Remove old and store new tokens
                var refreshTokenKey = CacheKeys.Authentication.RefreshToken(userId);
                var oldTokenToUserKey = CacheKeys.Authentication.TokenToUser(oldRefreshToken);
                var newTokenToUserKey = CacheKeys.Authentication.TokenToUser(newRefreshToken);
                var ttl = CacheKeys.Duration.RefreshToken;

                // Parallel operations for better performance
                var operations = Task.WhenAll(
                    _cacheService.RemoveAsync(oldTokenToUserKey), // Remove old token mapping
                    _cacheService.SetAsync(refreshTokenKey, newRefreshToken, ttl), // Store new token for user
                    _cacheService.SetAsync(newTokenToUserKey, userId, ttl) // Store new reverse mapping
                );

                await operations;

                _logger.LogDebug("Refresh token rotated successfully for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating refresh token for user {UserId}", userId);
                throw;
            }
        }

        public async Task<User?> GetUserForTokenRefreshAsync(Guid userId)
        {
            return await _dbContext.Users
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => new User
                {
                    Id = u.Id,
                    SupabaseUid = u.SupabaseUid,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    IsVerified = u.IsVerified,
                    IsOnboarding = u.IsOnboarding,
                })
                .FirstOrDefaultAsync();
        }

        #endregion

        #region Private Helper Methods
        private static List<Claim> CreateUserClaims(User user, string jwtId, DateTime issuedAt)
        {
            return new List<Claim>
            {
                // Standard claims
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, jwtId),
                new(JwtRegisteredClaimNames.Iat,
                    ((DateTimeOffset)issuedAt).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                
                // Custom claims
                new("user_id", user.Id.ToString()),
                new("supabase_uid", user.SupabaseUid.ToString()),
                new("first_name", user.FirstName ?? string.Empty),
                new("last_name", user.LastName ?? string.Empty),
                new("is_verified", user.IsVerified.ToString().ToLower()),
                new("is_onboarding", user.IsOnboarding.ToString().ToLower())
            };
        }

        private static List<Claim> CreateUserClaims(UserDTO user, string jwtId, DateTime issuedAt)
        {
            return new List<Claim>
            {
                // Standard claims
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, jwtId),
                new(JwtRegisteredClaimNames.Iat,
                    ((DateTimeOffset)issuedAt).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                
                // Custom claims
                new("user_id", user.Id.ToString()),
                new("first_name", user.FirstName ?? string.Empty),
                new("last_name", user.LastName ?? string.Empty),
                new("is_verified", user.IsVerified.ToString().ToLower()),
                new("is_onboarding", user.IsOnboarding.ToString().ToLower())
            };
        }

        private TokenValidationParameters CreateNonLifetimeValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Key difference
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = _signingKey,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        }

        private static bool IsValidJwtToken(SecurityToken token)
        {
            return token is JwtSecurityToken jwtToken &&
                   jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);
        }
        #endregion
    }
}
