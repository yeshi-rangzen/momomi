using MomomiAPI.Common.Results;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<EmailVerificationResult> SendOTPCode(string email);
        Task<EmailVerificationResult> VerifyOTPCode(string email, string code);
        Task<EmailVerificationResult> ResendOTPCode(string email);
        Task<RegistrationResult> RegisterNewUser(RegistrationRequest request);
        Task<LoginResult> LoginWithEmailCode(string email, string code);

    }
}
