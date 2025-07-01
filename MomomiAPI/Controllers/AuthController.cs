using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Send OTP to email address for verification
        /// </summary>
        [HttpPost("send-otp")]
        [EnableRateLimiting("OtpPolicy")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _authService.SendOtpAsync(request);

                if (!result.Success)
                    return BadRequest(new { message = result.Error, remainingAttempts = result.RemainingAttempts });

                return Ok(new
                {
                    message = result.Message,
                    expiresAt = result.ExpiresAt,
                    remainingAttempts = result.RemainingAttempts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP to {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Register a new user with OTP verification
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Register([FromBody] RegisterWithOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Validate age (must be 18+)
                var age = DateTime.UtcNow.Year - request.DateOfBirth.Year;
                if (age < 18)
                    return BadRequest(new { message = "You must be at least 18 years old to register" });

                var result = await _authService.RegisterWithOtpAsync(request);

                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                return Ok(new
                {
                    message = "Registration successful",
                    user = new
                    {
                        id = result.User?.Id,
                        email = result.User?.Email,
                        firstName = result.User?.FirstName,
                        lastName = result.User?.LastName,
                        isActive = result.User?.IsActive
                    },
                    token = new
                    {
                        accessToken = result.AccessToken,
                        refreshToken = result.RefreshToken,
                        expiresAt = result.ExpiresAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Login user with OTP verification
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginWithOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _authService.VerifyOtpAndLoginAsync(request);

                if (!result.Success)
                    return Unauthorized(new { message = result.Error });

                return Ok(new
                {
                    message = "Login successful",
                    user = new
                    {
                        id = result.User?.Id,
                        email = result.User?.Email,
                        firstName = result.User?.FirstName,
                        lastName = result.User?.LastName,
                        isActive = result.User?.IsActive,
                        lastActive = result.User?.LastActive
                    },
                    token = new
                    {
                        accessToken = result.AccessToken,
                        refreshToken = result.RefreshToken,
                        expiresAt = result.ExpiresAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login for {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Resend OTP to email address
        /// </summary>
        [HttpPost("resend-otp")]
        [EnableRateLimiting("OtpPolicy")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _authService.ResendOtpAsync(request);

                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                return Ok(new
                {
                    message = result.Message,
                    expiresAt = result.ExpiresAt,
                    remainingAttempts = result.RemainingAttempts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending OTP to {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if email is already registered
        /// </summary>
        [HttpPost("check-email")]
        [EnableRateLimiting("GeneralPolicy")]
        public async Task<IActionResult> CheckEmail([FromBody] SendOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var isRegistered = await _authService.IsEmailRegisteredAsync(request.Email);

                return Ok(new
                {
                    email = request.Email,
                    isRegistered = isRegistered,
                    action = isRegistered ? "login" : "register"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email {Email}", request.Email);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _authService.RefreshTokenAsync(request.RefreshToken);

                if (!result.Success)
                    return Unauthorized(new { message = result.Error });

                return Ok(new
                {
                    message = "Token refreshed successfully",
                    token = new
                    {
                        accessToken = result.AccessToken,
                        refreshToken = result.RefreshToken,
                        expiresAt = result.ExpiresAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Logout user
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var result = await _authService.LogoutAsync(token);

                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user logout");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        /// <summary>
        /// Verify token and get current user
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var user = await _authService.GetUserFromTokenAsync(token);

                if (user == null)
                    return Unauthorized(new { message = "Invalid token" });

                return Ok(new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    isActive = user.IsActive,
                    lastActive = user.LastActive,
                    createdAt = user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Revoke all user sessions (security feature)
        /// </summary>
        [HttpPost("revoke-sessions")]
        [Authorize]
        public async Task<IActionResult> RevokeSessions()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var result = await _authService.RevokeUserSessionsAsync(userId.Value);

                if (!result)
                    return BadRequest(new { message = "Failed to revoke sessions" });

                return Ok(new { message = "All sessions revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking user sessions");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
