using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace MomomiAPI.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ILogger<RedisHealthCheck> _logger;

        public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisHealthCheck> logger)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connectionMultiplexer == null || !_connectionMultiplexer.IsConnected)
                {
                    return HealthCheckResult.Unhealthy("Redis connection not available");
                }

                var database = _connectionMultiplexer.GetDatabase();
                var pingResult = await database.PingAsync();

                var data = new Dictionary<string, object>
                {
                    { "ping_time_ms", pingResult.TotalMilliseconds },
                    { "connected_endpoints", _connectionMultiplexer.GetEndPoints().Length },
                    { "is_connected", _connectionMultiplexer.IsConnected }
                };

                return HealthCheckResult.Healthy("Redis is responsive", data);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection failed during health check");
                return HealthCheckResult.Unhealthy("Redis connection failed", ex);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout during health check");
                return HealthCheckResult.Degraded("Redis is slow to respond", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Redis health check");
                return HealthCheckResult.Unhealthy("Redis health check failed", ex);
            }
        }
    }
}