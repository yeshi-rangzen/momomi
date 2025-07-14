using MomomiAPI.Common.Results;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Services.Interfaces
{
    public interface ITokenManagementService
    {
        Task<OperationResult<LoginResult>> RefreshUserToken(string refreshToken);
        Task<OperationResult> InvalidateUserToken(string accessToken);
        Task<OperationResult> InvalidateAllUserTokens(Guid userId);
        Task<OperationResult<User>> ValidateAndGetUser(string accessToken);
    }
}