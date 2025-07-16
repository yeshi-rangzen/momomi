using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class TokenManagementService : ITokenManagementService
    {
        private readonly IJwtService _jwtService;
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<TokenManagementService> _logger;

        public TokenManagementService(
            IJwtService jwtService,
            MomomiDbContext dbContext,
            ILogger<TokenManagementService> logger)
        {
            _jwtService = jwtService;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<OperationResult<LoginResult>> RefreshUserToken(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Processing token refresh request");

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return OperationResult<LoginResult>.ValidationFailure("Refresh token is required.");
                }

                // Get user ID from refresh token
                var userId = await _jwtService.GetUserIdByRefreshTokenAsync(refreshToken);
                if (!userId.HasValue)
                {
                    _logger.LogWarning("Invalid refresh token provided");
                    return OperationResult<LoginResult>.Unauthorized("Invalid refresh token.");
                }

                // Verify user exists and is active
                var user = await _dbContext.Users
                    .Where(u => u.Id == userId.Value && u.IsActive)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("User not found or inactive for refresh token. UserId: {UserId}", userId.Value);
                    return OperationResult<LoginResult>.NotFound("User not found or account is inactive.");
                }

                // Generate new tokens
                var newAccessToken = _jwtService.GenerateAccessToken(user);
                var newRefreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Revoke old refresh token and store new one
                await _jwtService.RevokeRefreshTokenAsync(user.Id);
                await _jwtService.StoreRefreshTokenAsync(user.Id, newRefreshToken, refreshExpiry);

                // Update last active
                user.LastActive = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                var loginResult = LoginResult.Success(
                    user,
                    newAccessToken,
                    newRefreshToken,
                    DateTime.UtcNow.AddHours(1)
                );

                _logger.LogInformation("Token refreshed successfully for user {UserId}", user.Id);
                return OperationResult<LoginResult>.Successful(loginResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return OperationResult<LoginResult>.Failed("Token refresh failed. Please try again.");
            }
        }

        public async Task<OperationResult> InvalidateUserToken(string accessToken)
        {
            try
            {
                _logger.LogInformation("Processing token invalidation request");

                if (string.IsNullOrEmpty(accessToken))
                {
                    return OperationResult.ValidationFailure("Access token is required.");
                }

                // Clean the token (remove 'Bearer ' if present)
                var cleanToken = accessToken.Replace("Bearer ", "").Trim();

                // Get token info to blacklist it
                var tokenInfo = _jwtService.GetTokenInfo(cleanToken);
                if (tokenInfo.HasValue)
                {
                    await _jwtService.BlacklistTokenAsync(tokenInfo.Value.jti, tokenInfo.Value.expiry);
                }

                // Get user from token and revoke refresh token
                var principal = _jwtService.ValidateToken(cleanToken, validateLifetime: false);
                var userIdClaim = principal?.FindFirst("user_id")?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    await _jwtService.RevokeRefreshTokenAsync(userId);
                    _logger.LogInformation("Successfully invalidated tokens for user {UserId}", userId);
                }

                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating token");
                return OperationResult.Failed("Token invalidation failed. Please try again.");
            }
        }

        public async Task<OperationResult> InvalidateAllUserTokens(Guid userId)
        {
            try
            {
                _logger.LogInformation("Processing invalidation of all tokens for user {UserId}", userId);

                // Verify user exists
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    return OperationResult.NotFound("User not found.");
                }

                // Revoke all user tokens
                await _jwtService.RevokeAllUserTokensAsync(userId);

                _logger.LogInformation("Successfully invalidated all tokens for user {UserId}", userId);
                return OperationResult.Successful().WithMetadata("revoked_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all tokens for user {UserId}", userId);
                return OperationResult.Failed("Failed to invalidate user sessions. Please try again.");
            }
        }

        public async Task<OperationResult<User>> ValidateAndGetUser(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    return OperationResult<User>.ValidationFailure("Access token is required.");
                }

                // Clean the token (remove 'Bearer ' if present)
                var cleanToken = accessToken.Replace("Bearer ", "").Trim();

                // Validate token
                var principal = _jwtService.ValidateToken(cleanToken, validateLifetime: true);
                if (principal == null)
                {
                    return OperationResult<User>.Unauthorized("Invalid or expired token.");
                }

                // Extract user ID
                var userIdClaim = principal.FindFirst("user_id")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return OperationResult<User>.Unauthorized("Invalid token claims.");
                }

                // Check if token is blacklisted
                var jtiClaim = principal.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jtiClaim))
                {
                    var isBlacklisted = await _jwtService.IsTokenBlacklistedAsync(jtiClaim);
                    if (isBlacklisted)
                    {
                        return OperationResult<User>.Unauthorized("Token has been revoked.");
                    }
                }

                // Get user from database
                var user = await _dbContext.Users
                    .Include(u => u.Photos)
                    .Include(u => u.Preferences)
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null)
                {
                    return OperationResult<User>.NotFound("User not found or account is inactive.");
                }

                // Check for user-level token revocation
                var issuedAtClaim = principal.FindFirst("iat")?.Value;
                if (long.TryParse(issuedAtClaim, out var issuedAt))
                {
                    // This would require additional cache logic to check if all user tokens
                    // were revoked after this token was issued
                    // Implementation depends on your specific revocation strategy
                }

                return OperationResult<User>.Successful(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token and retrieving user");
                return OperationResult<User>.Failed("Token validation failed.");
            }
        }
    }
}