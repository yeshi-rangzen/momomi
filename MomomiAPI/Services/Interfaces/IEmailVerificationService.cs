using MomomiAPI.Common.Results;

namespace MomomiAPI.Services.Interfaces
{
    public interface IEmailVerificationService
    {
        Task<EmailVerificationResult> SendVerificationCode(string email);
        Task<EmailVerificationResult> VerifyEmailCode(string email, string code);
        Task<EmailVerificationResult> ResendVerificationCode(string email);
        Task<bool> IsEmailAlreadyRegistered(string email);
    }
}
