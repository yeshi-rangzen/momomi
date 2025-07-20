using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IUserRegistrationService _userRegistrationService;
        private readonly IUserLoginService _userLoginService;
        private readonly ITokenManagementService _tokenManagementService;
        private readonly IAnalyticsService _analyticsService;

        public AuthController(
            IEmailVerificationService emailVerificationService,
            IUserRegistrationService userRegistrationService,
            IUserLoginService userLoginService,
            ITokenManagementService tokenManagementService,
            IAnalyticsService analyticsService,
            ILogger<AuthController> logger) : base(logger)
        {
            _emailVerificationService = emailVerificationService;
            _userRegistrationService = userRegistrationService;
            _userLoginService = userLoginService;
            _tokenManagementService = tokenManagementService;
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Send verification code to email address
        /// </summary>
        [HttpPost("send-verification-code")]
        [EnableRateLimiting("OtpPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<EmailVerificationResult>> SendVerificationCode([FromBody] SendOtpRequest request)
        {
            LogControllerAction(nameof(SendVerificationCode), new { request.Email });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _emailVerificationService.SendVerificationCode(request.Email);

            // Track email verification sent
            if (result.Success)
            {
                _ = Task.Run(() => _analyticsService.TrackEmailVerificationSentAsync(
                    request.Email,
                    result.RemainingAttempts ?? 0));
            }

            return HandleAuthResult(result);
        }

        /// <summary>
        /// Verify email code for registration process
        /// </summary>
        [HttpPost("verify-email-code")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailCode([FromBody] VerifyOtpRequest request)
        {
            LogControllerAction(nameof(VerifyEmailCode), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _emailVerificationService.VerifyEmailCode(request.Email, request.Otp);
            return HandleAuthResult(result);
        }


        /// <summary>
        /// Complete user registration with verified email
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<RegistrationResult>> RegisterUser([FromBody] CompleteRegistrationRequest request)
        {
            LogControllerAction(nameof(RegisterUser), new { request.Email, request.FirstName });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userRegistrationService.RegisterNewUser(request);

            // Track successful registration
            if (((OperationResult)result).Success && result.Data != null)
            {
                var analyticsData = new UserRegistrationData
                {
                    Email = request.Email,
                    Age = DateTime.UtcNow.Year - request.DateOfBirth.Year,
                    Gender = request.Gender,
                    Heritage = request.Heritage ?? new List<HeritageType>(),
                    Religion = request.Religion ?? new List<ReligionType>(),
                    Languages = request.LanguagesSpoken ?? new List<LanguageType>(),
                    Hometown = request.Hometown,
                    RegistrationMethod = "email",
                    RegistrationTimestamp = DateTime.UtcNow
                };

                // Fire and forget analytics tracking
                _ = Task.Run(() => _analyticsService.TrackUserRegistrationAsync(result.Data.Id, analyticsData));
            }

            return HandleAuthResult(result);
        }

        /// <summary>
        /// Login user with email and verification code
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResult>> LoginUser([FromBody] LoginWithOtpRequest request)
        {
            LogControllerAction(nameof(LoginUser), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _userLoginService.LoginWithEmailCode(request.Email, request.Otp);

            // Track successful login
            if (((OperationResult)result).Success && result.Data != null)
            {
                var loginData = new LoginData
                {
                    Email = request.Email,
                    LoginMethod = "email_otp",
                    DaysSinceLastLogin = CalculateDaysSinceLastLogin(result.Data.LastActive),
                    LoginTimestamp = DateTime.UtcNow
                };

                // Fire and forget analytics tracking
                _ = Task.Run(() => _analyticsService.TrackUserLoginAsync(result.Data.Id, loginData));
            }

            return HandleAuthResult(result);
        }

        /// <summary>
        /// Resend verification code to email
        /// </summary>
        [HttpPost("resend-verification-code")]
        [EnableRateLimiting("OtpPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<EmailVerificationResult>> ResendVerificationCode([FromBody] ResendOtpRequest request)
        {
            LogControllerAction(nameof(ResendVerificationCode), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _emailVerificationService.ResendVerificationCode(request.Email);
            return HandleAuthResult(result);
        }

        /// <summary>
        /// Check if email is already registered
        /// </summary>
        [HttpPost("check-email")]
        [EnableRateLimiting("GeneralPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult> CheckEmailRegistration([FromBody] SendOtpRequest request)
        {
            LogControllerAction(nameof(CheckEmailRegistration), new { request.Email });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var isRegistered = await _emailVerificationService.IsEmailAlreadyRegistered(request.Email);

            return Ok(new
            {
                email = request.Email,
                isRegistered = isRegistered,
                suggestedAction = isRegistered ? "login" : "register"
            });
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [EnableRateLimiting("AuthPolicy")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResult>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            LogControllerAction(nameof(RefreshToken));

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _tokenManagementService.RefreshUserToken(request.RefreshToken);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Logout current user
        /// </summary>
        [HttpPost("logout")]
        public async Task<ActionResult> LogoutUser()
        {
            LogControllerAction(nameof(LogoutUser));

            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var result = await _tokenManagementService.InvalidateUserToken(token);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get current user information
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<User>> GetCurrentUser()
        {
            LogControllerAction(nameof(GetCurrentUser));

            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var result = await _tokenManagementService.ValidateAndGetUser(token);
            return HandleOperationResult(result);
        }

        private static int CalculateDaysSinceLastLogin(DateTime lastActive)
        {
            return (int)(DateTime.UtcNow - lastActive).TotalDays;
        }
        /// <summary>
        /// Revoke all user sessions (security feature)
        /// </summary>
        //[HttpPost("revoke-all-sessions")]
        //public async Task<ActionResult> RevokeAllSessions()
        //{
        //    var userIdResult = GetCurrentUserIdOrUnauthorized();
        //    if (userIdResult.Result != null) return userIdResult.Result;

        //    LogControllerAction(nameof(RevokeAllSessions));

        //    var result = await _tokenManagementService.InvalidateAllUserTokens(userIdResult.Value);
        //    return HandleOperationResult(result);
        //}
    }
}
