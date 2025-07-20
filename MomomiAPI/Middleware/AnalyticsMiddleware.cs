using MomomiAPI.Services.Interfaces;
using System.Diagnostics;

namespace MomomiAPI.Middleware
{
    public class AnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnalyticsMiddleware> _logger;

        public AnalyticsMiddleware(RequestDelegate next, ILogger<AnalyticsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAnalyticsService analyticsService)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Track API performance for slow requests or errors
                if (stopwatch.Elapsed.TotalSeconds > 2 || context.Response.StatusCode >= 400)
                {
                    var userId = GetUserIdFromContext(context);
                    var endpoint = $"{context.Request.Method} {context.Request.Path}";

                    _ = Task.Run(() => analyticsService.TrackApiPerformanceAsync(
                        endpoint,
                        stopwatch.Elapsed,
                        context.Response.StatusCode,
                        userId));
                }
            }
        }

        private static Guid? GetUserIdFromContext(HttpContext context)
        {
            var userIdClaim = context.User?.FindFirst("user_id")?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}