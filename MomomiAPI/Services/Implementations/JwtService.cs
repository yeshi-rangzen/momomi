using Microsoft.IdentityModel.Tokens;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MomomiAPI.Services.Implementations
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ICacheService _cacheService;
        private readonly ILogger<JwtService> _logger;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public JwtService(
            IConfiguration configuration,
            ICacheService cacheService,
            ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _cacheService = cacheService;
            _logger = logger;

            var secretKey = _configuration["Jwt:SecretKey"] ??
                          throw new ArgumentNullException("JWT Secret Key is required");

            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        }

        public string GenerateAccessToken(User user)
        {
            try
            {
                var jwtId = Guid.NewGuid().ToString();
                var issuedAt = DateTime.UtcNow;
                var expires = issuedAt.AddMinutes(int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "60"));

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new("user_id", user.Id.ToString()),
                    new("supabase_uid", user.SupabaseUid.ToString()),
                    new(ClaimTypes.Email, user.Email),
                    new("first_name", user.FirstName ?? string.Empty),
                    new("last_name", user.LastName ?? string.Empty),
                    new("is_verified", user.IsVerified.ToString().ToLower()),
                    new(JwtRegisteredClaimNames.Jti, jwtId),
                    new(JwtRegisteredClaimNames.Iat, ((DateTimeOffset)issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                    new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new(JwtRegisteredClaimNames.Email, user.Email)
                };

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expires,
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!)),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);

                _logger.LogDebug("Generated access token for user {UserId} with JTI {Jti}", user.Id, jwtId);

                return tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
                throw;
            }
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                // Create a copy of validation parameters to modify lifetime validation
                var validationParameters = _tokenValidationParameters.Clone();
                validationParameters.ValidateLifetime = validateLifetime;

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                // Additional validation
                if (validatedToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
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

        public async Task<bool> IsTokenBlacklistedAsync(string jti)
        {
            try
            {
                var blacklistKey = $"blacklist_token_{jti}";
                var isBlacklisted = await _cacheService.GetAsync<bool?>(blacklistKey);
                return isBlacklisted ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token blacklist for JTI {Jti}", jti);
                return false; // Fail open for availability
            }
        }

        public async Task BlacklistTokenAsync(string jti, DateTime expiry)
        {
            try
            {
                var blacklistKey = $"blacklist_token_{jti}";
                var ttl = expiry > DateTime.UtcNow ? expiry - DateTime.UtcNow : TimeSpan.FromMinutes(1);

                await _cacheService.SetAsync(blacklistKey, true, ttl);

                _logger.LogDebug("Blacklisted token with JTI {Jti} until {Expiry}", jti, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blacklisting token with JTI {Jti}", jti);
                throw;
            }
        }

        public async Task<string?> GetStoredRefreshTokenAsync(Guid userId)
        {
            try
            {
                var refreshTokenKey = $"refresh_token_{userId}";
                return await _cacheService.GetAsync<string>(refreshTokenKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving refresh token for user {UserId}", userId);
                return null;
            }
        }

        public async Task<Guid?> GetUserIdByRefreshTokenAsync(string refreshToken)
        {
            try
            {
                var tokenToUserKey = $"token_to_user_{refreshToken}";
                var userId = await _cacheService.GetAsync<Guid?>(tokenToUserKey);
                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving User Id for {RefreshToken}", refreshToken);
                return null;
            }
        }

        public async Task StoreRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiry)
        {
            try
            {
                var refreshTokenKey = $"refresh_token_{userId}";
                var tokenToUserKey = $"token_to_user_{refreshToken}"; // New reverse mapping
                var ttl = expiry > DateTime.UtcNow ? expiry - DateTime.UtcNow : TimeSpan.FromDays(30);

                // Store both mappings
                await _cacheService.SetAsync(refreshTokenKey, refreshToken, ttl);
                await _cacheService.SetAsync(tokenToUserKey, userId, ttl); // Store reverse mapping

                _logger.LogDebug("Stored refresh token for user {UserId} until {Expiry}", userId, expiry);
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
                // Get the current refresh token to remove reverse mapping
                var refreshTokenKey = $"refresh_token_{userId}";
                var currentToken = await _cacheService.GetAsync<string>(refreshTokenKey);

                // Remove both mappings
                await _cacheService.RemoveAsync(refreshTokenKey);

                if (!string.IsNullOrEmpty(currentToken))
                {
                    var tokenToUserKey = $"token_to_user_{currentToken}";
                    await _cacheService.RemoveAsync(tokenToUserKey);
                }

                _logger.LogDebug("Revoked refresh token for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token for user {UserId}", userId);
                throw;
            }
        }

        public async Task RevokeAllUserTokensAsync(Guid userId)
        {
            try
            {
                // Revoke refresh token
                await RevokeRefreshTokenAsync(userId);

                // Add user to global revocation list with current timestamp
                // All tokens issued before this timestamp will be considered invalid
                var revocationKey = $"user_token_revocation_{userId}";
                var revocationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                await _cacheService.SetAsync(revocationKey, revocationTimestamp, TimeSpan.FromDays(7)); // Keep for 7 days

                _logger.LogInformation("Revoked all tokens for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
                throw;
            }
        }

        public (string jti, DateTime expiry)? GetTokenInfo(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);

                var jti = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti))
                    return null;

                return (jti, jsonToken.ValidTo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting token info");
                return null;
            }
        }
    }
}