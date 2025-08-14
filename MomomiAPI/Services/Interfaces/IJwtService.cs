using MomomiAPI.Common.Results;
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
        Task<Guid?> GetUserIdFromRefreshTokenAsync(string refreshToken);
        Task CacheRefreshTokenAsync(Guid userId, string refreshToken);
        Task RevokeRefreshTokenAsync(Guid userId);
        Task RevokeAllUserTokensAsync(Guid userId);
        Task<RefreshTokenResult> RefreshUserTokenAsync(string refreshToken);
    }
}
