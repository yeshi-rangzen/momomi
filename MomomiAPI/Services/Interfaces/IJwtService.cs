using MomomiAPI.Models.Entities;
using System.Security.Claims;

namespace MomomiAPI.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
        Task<bool> IsTokenBlacklistedAsync(string jti);
        Task BlacklistTokenAsync(string jti, DateTime expiry);
        Task<string?> GetStoredRefreshTokenAsync(Guid userId);
        Task<Guid?> GetUserIdByRefreshTokenAsync(string refreshToken);
        Task StoreRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiry);
        Task RevokeRefreshTokenAsync(Guid userId);
        Task RevokeAllUserTokensAsync(Guid userId);
        (string jti, DateTime expiry)? GetTokenInfo(string token);
    }
}
