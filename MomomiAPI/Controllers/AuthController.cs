using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomomiAPI.Common.Results;
using MomomiAPI.Services.Interfaces;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        private readonly IJwtService _jwtService;
        //private readonly IAnalyticsService _analyticsService;

        public AuthController(
            IAuthService authService,
            IJwtService jwtService,
            //IAnalyticsService analyticsService,
            ILogger<AuthController> logger) : base(logger)
        {
            _authService = authService;
            //_analyticsService = analyticsService;
            _jwtService = jwtService;
        }

        [HttpPost("send-otp-code")]
        [EnableRateLimiting("OtpPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<EmailVerificationData>>> SendOTPCode([FromBody] SendOtpRequest request)
        {
            LogControllerAction(nameof(SendOTPCode), new { request.Email });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.SendOTPCode(request.Email);

            // Track email verification sent
            //if (result.Success)
            //{
            //    _ = Task.Run(() => _analyticsService.TrackEmailVerificationSentAsync(
            //        request.Email,
            //        result.Data.RemainingAttempts ?? 0));
            //}

            return HandleOperationResult(result);
        }

        [HttpPost("verify-otp-code")]
        [EnableRateLimiting("OtpPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<EmailVerificationData>>> VerifyOTPCode([FromBody] VerifyOtpRequest request)
        {
            LogControllerAction(nameof(VerifyOTPCode), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.VerifyOTPCode(request.Email, request.Otp);
            return HandleOperationResult(result);
        }

        [HttpPost("resend-otp-code")]
        [EnableRateLimiting("OtpPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<EmailVerificationData>>> ResendOTPCode([FromBody] ResendOtpRequest request)
        {
            LogControllerAction(nameof(ResendOTPCode), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResendOTPCode(request.Email);
            return HandleOperationResult(result);
        }

        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<RegistrationData>>> RegisterUser([FromBody] RegistrationRequest request)
        {
            LogControllerAction(nameof(RegisterUser), new { request.Email, request.FirstName });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterNewUser(request);

            // Track successful registration
            //if (((OperationResult)result).Success && result.Data != null)
            //{
            //    var analyticsData = new UserRegistrationData
            //    {
            //        Email = request.Email,
            //        Age = DateTime.UtcNow.Year - request.DateOfBirth.Year,
            //        Gender = request.Gender,
            //        Heritage = request.Heritage ?? new List<HeritageType>(),
            //        Religion = request.Religion ?? new List<ReligionType>(),
            //        Languages = request.LanguagesSpoken ?? new List<LanguageType>(),
            //        Hometown = request.Hometown,
            //        RegistrationMethod = "email",
            //        RegistrationTimestamp = DateTime.UtcNow
            //    };

            //    // Fire and forget analytics tracking
            //    _ = Task.Run(() => _analyticsService.TrackUserRegistrationAsync(result.Data.User.Id, analyticsData));
            //}

            return HandleOperationResult(result);
        }

        [HttpPost("login")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<LoginData>>> LoginUser([FromBody] LoginWithOtpRequest request)
        {
            LogControllerAction(nameof(LoginUser), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginWithEmailCode(request.Email, request.Otp);

            // Track successful login
            //if (((OperationResult)result).Success && result.Data != null)
            //{
            //    var loginData = new LoginDataAnalytics
            //    {
            //        Email = request.Email,
            //        LoginMethod = "email_otp",
            //        DaysSinceLastLogin = CalculateDaysSinceLastLogin(result.Data.User.LastActive),
            //        LoginTimestamp = DateTime.UtcNow
            //    };

            //    // Fire and forget analytics tracking
            //    _ = Task.Run(() => _analyticsService.TrackUserLoginAsync(result.Data.User.Id, loginData));
            //}

            return HandleOperationResult(result);
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<OperationResult<RefreshTokenData>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                LogControllerAction(nameof(RefreshToken));

                if (!ModelState.IsValid)
                    return ValidationError("Invalid request data");

                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    return ValidationError("Refresh token is required");

                // Process token refresh
                var result = await _jwtService.RefreshUserTokenAsync(request.RefreshToken);

                if (result.Success)
                {
                    // Add security headers for token response
                    Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
                    Response.Headers.Append("Pragma", "no-cache");
                }

                return HandleOperationResult(result);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Token refresh failed");
            }
        }

        [HttpPost("logout")]
        public async Task<ActionResult> LogoutUser()
        {
            LogControllerAction(nameof(LogoutUser));

            var userId = GetCurrentUserId();

            // Only revoke refresh token
            if (userId == null)
            {
                return Unauthorized("User not authenticated");
            }

            await _jwtService.RevokeRefreshTokenAsync((Guid)userId);

            // Blacklist current access token for immediate effect
            //var token = ExtractTokenFromRequest();
            //if (token != null)
            //{
            //    var tokenInfo = _jwtService.GetTokenInfo(token);
            //    if (tokenInfo != null)
            //    {
            //        await _jwtService.BlacklistTokenAsync(tokenInfo.Value.jti, tokenInfo.Value.expiry);
            //    }
            //}

            return Ok(new { message = "Logged out successfully" });
        }

        /// Extract token from Authorization header
        private string? ExtractTokenFromRequest()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            return authHeader.Substring("Bearer ".Length).Trim();
        }

        private static int CalculateDaysSinceLastLogin(DateTime lastActive)
        {
            return (int)(DateTime.UtcNow - lastActive).TotalDays;
        }
    }
}
