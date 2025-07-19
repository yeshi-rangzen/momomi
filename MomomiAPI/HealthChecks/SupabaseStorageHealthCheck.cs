using Microsoft.Extensions.Diagnostics.HealthChecks;
using Supabase.Storage;

namespace MomomiAPI.HealthChecks
{
    public class SupabaseStorageHealthCheck : IHealthCheck
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<SupabaseStorageHealthCheck> _logger;

        public SupabaseStorageHealthCheck(Supabase.Client supabaseClient, ILogger<SupabaseStorageHealthCheck> logger)
        {
            _supabaseClient = supabaseClient;
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

                var files = await _supabaseClient.Storage
                    .From("user-photos")
                    .List("", new SearchOptions { Limit = 1 });

                return HealthCheckResult.Healthy("User photos bucket accessible");
            }
            catch (Exception ex) when (ex.Message.Contains("401"))
            {
                return HealthCheckResult.Degraded("Storage accessible but auth issues");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Storage not accessible", ex);
            }
        }
    }
}
