using MomomiAPI.Common.Results;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserLoginService
    {
        Task<LoginResult> LoginWithEmailCode(string email, string code);
    }
}