using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MomomiAPI.HealthChecks
{
    public class SupabaseHealthCheck : IHealthCheck
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SupabaseHealthCheck> _logger;

        public SupabaseHealthCheck(
            Supabase.Client supabaseClient,
            IConfiguration configuration,
            ILogger<SupabaseHealthCheck> logger)
        {
            _supabaseClient = supabaseClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_supabaseClient == null)
                {
                    return HealthCheckResult.Unhealthy("Supabase client not available");
                }

                // Get configuration values to verify setup
                var supabaseUrl = _configuration["Supabase:Url"];
                var supabaseKey = _configuration["Supabase:Key"];

                if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
                {
                    return HealthCheckResult.Unhealthy("Supabase configuration missing");
                }

                // Test if we can access the auth service
                var authInitialized = _supabaseClient.Auth != null;

                // Get current session info (this doesn't require network call)
                var currentSession = _supabaseClient.Auth.CurrentSession;
                var currentUser = _supabaseClient.Auth.CurrentUser;

                // Simple connectivity test - try to get settings (lightweight operation)
                try
                {
                    // Corrected: Call Settings() without parameters
                    var authSettings = await _supabaseClient.Auth.Settings();

                    var data = new Dictionary<string, object>
                    {
                        { "supabase_url", supabaseUrl },
                        { "auth_initialized", authInitialized },
                        { "has_current_session", currentSession != null },
                        { "has_current_user", currentUser != null },
                        { "auth_settings_accessible", authSettings != null }
                    };

                    return HealthCheckResult.Healthy("Supabase client is configured and accessible", data);
                }
                catch (Exception settingsEx)
                {
                    // If settings call fails, still consider it healthy if basic config is present
                    _logger.LogWarning(settingsEx, "Supabase settings call failed, but basic configuration is present");

                    var data = new Dictionary<string, object>
                    {
                        { "supabase_url", supabaseUrl },
                        { "auth_initialized", authInitialized },
                        { "has_current_session", currentSession != null },
                        { "has_current_user", currentUser != null },
                        { "auth_settings_accessible", false },
                        { "settings_error", settingsEx.Message }
                    };

                    return HealthCheckResult.Degraded("Supabase client configured but connectivity issues", null, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Supabase health check failed");
                return HealthCheckResult.Unhealthy("Supabase health check failed", ex);
            }
        }
    }
}