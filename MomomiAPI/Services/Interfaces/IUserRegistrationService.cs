using MomomiAPI.Common.Results;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserRegistrationService
    {
        Task<RegistrationResult> RegisterNewUser(CompleteRegistrationRequest request);
    }
}