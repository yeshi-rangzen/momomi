using Microsoft.Extensions.Diagnostics.HealthChecks;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.HealthChecks
{
    public class PostHogHealthCheck : IHealthCheck
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<PostHogHealthCheck> _logger;

        public PostHogHealthCheck(IAnalyticsService analyticsService, ILogger<PostHogHealthCheck> logger)
        {
            _analyticsService = analyticsService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var isHealthy = await _analyticsService.IsHealthyAsync();

                if (isHealthy)
                {
                    return HealthCheckResult.Healthy("PostHog analytics service is responsive", new Dictionary<string, object>
                    {
                        { "service", "PostHog" },
                        { "status", "healthy" },
                        { "checked_at", DateTime.UtcNow }
                    });
                }
                else
                {
                    return HealthCheckResult.Unhealthy("PostHog analytics service is not responding", null, new Dictionary<string, object>
                    {
                        { "service", "PostHog" },
                        { "status", "unhealthy" },
                        { "checked_at", DateTime.UtcNow }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostHog health check failed");
                return HealthCheckResult.Unhealthy("PostHog health check failed", ex, new Dictionary<string, object>
                {
                    { "service", "PostHog" },
                    { "status", "error" },
                    { "error", ex.Message },
                    { "checked_at", DateTime.UtcNow }
                });
            }
        }
    }
}