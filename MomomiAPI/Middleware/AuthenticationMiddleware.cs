using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IUserService userService)
        {
            try
            {
                // The JWT middleware will handle most of the work
                // This middleware just adds any additional user context if needed

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Update last active time (optional, consider caching this)
                        if (Guid.TryParse(userId, out var userGuid))
                        {
                            // You could implement a background service to update last active
                            // instead of doing it on every request
                            _logger.LogDebug("Authenticated user: {UserId}", userGuid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in authentication middleware: {Message}", ex.Message);
            }

            await _next(context);
        }
    }
}
