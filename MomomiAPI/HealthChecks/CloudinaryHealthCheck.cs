using CloudinaryDotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MomomiAPI.HealthChecks
{
    public class CloudinaryHealthCheck : IHealthCheck
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryHealthCheck> _logger;

        public CloudinaryHealthCheck(Cloudinary cloudinary, ILogger<CloudinaryHealthCheck> logger)
        {
            _cloudinary = cloudinary;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cloudinary == null)
                {
                    return HealthCheckResult.Unhealthy("Cloudinary client not available");
                }

                // Check if client is properly configured
                var isConfigured = !string.IsNullOrEmpty(_cloudinary.Api.Account.Cloud) &&
                                 !string.IsNullOrEmpty(_cloudinary.Api.Account.ApiKey);

                if (!isConfigured)
                {
                    return HealthCheckResult.Unhealthy("Cloudinary client not properly configured");
                }

                // Optional: Test a simple API call (ping)
                // Note: This makes an actual API call, so use sparingly
                // var pingResult = await _cloudinary.Api.PingAsync();

                var data = new Dictionary<string, object>
                {
                    { "cloud_name", _cloudinary.Api.Account.Cloud },
                    { "api_key_configured", !string.IsNullOrEmpty(_cloudinary.Api.Account.ApiKey) },
                    { "api_secret_configured", !string.IsNullOrEmpty(_cloudinary.Api.Account.ApiSecret) }
                };

                return HealthCheckResult.Healthy("Cloudinary client is configured", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary health check failed");
                return HealthCheckResult.Unhealthy("Cloudinary health check failed", ex);
            }
        }
    }
}