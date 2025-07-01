using MomomiAPI.Services.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace MomomiAPI.Services.Implementations
{
    public class UpstashCacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ILogger<UpstashCacheService> _logger;
        private readonly bool _isRedisAvailable;

        public UpstashCacheService(IConnectionMultiplexer redis, ILogger<UpstashCacheService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;

            try
            {
                _database = redis.GetDatabase();

                // Test Redis connection
                _database.Ping();
                _isRedisAvailable = true;
                _logger.LogInformation("Redis connection established successfully");
            }
            catch (Exception ex)
            {
                _isRedisAvailable = false;
                _logger.LogError(ex, "Redis is not available. Cache service will operate in fallback mode.");
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, returning default value for key: {Key}", key);
                return default(T);
            }

            try
            {
                var value = await _database.StringGetAsync(key);
                if (!value.HasValue)
                    return default(T);

                var result = JsonSerializer.Deserialize<T>(value!);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return result;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error getting cache key {Key}", key);
                return default(T);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error getting cache key {Key}", key);
                return default(T);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for cache key {Key}", key);
                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting cache key {Key}", key);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, skipping cache set for key: {Key}", key);
                return;
            }

            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, serializedValue, expiry);
                _logger.LogDebug("Cache set for key: {Key}", key);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error setting cache key {Key}", key);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error setting cache key {Key}", key);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for cache key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error setting cache key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, skipping cache removal for key: {Key}", key);
                return;
            }

            try
            {
                await _database.KeyDeleteAsync(key);
                _logger.LogDebug("Cache removed for key: {Key}", key);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error removing cache key {Key}", key);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error removing cache key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error removing cache key {Key}", key);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, returning false for key exists: {Key}", key);
                return false;
            }

            try
            {
                var exists = await _database.KeyExistsAsync(key);
                _logger.LogDebug("Cache exists check for key {Key}: {Exists}", key, exists);
                return exists;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error checking cache key exists {Key}", key);
                return false;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error checking cache key exists {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking cache key exists {Key}", key);
                return false;
            }
        }

        public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, skipping string cache set for key: {Key}", key);
                return;
            }

            try
            {
                await _database.StringSetAsync(key, value, expiry);
                _logger.LogDebug("String cache set for key: {Key}", key);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error setting string cache key {Key}", key);
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error setting string cache key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error setting string cache key {Key}", key);
            }
        }

        public async Task<string?> GetStringAsync(string key)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, returning null for string cache key: {Key}", key);
                return null;
            }

            try
            {
                var value = await _database.StringGetAsync(key);
                _logger.LogDebug("String cache get for key {Key}: {HasValue}", key, value.HasValue);
                return value.HasValue ? value.ToString() : null;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error getting string cache key {Key}", key);
                return null;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis timeout error getting string cache key {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting string cache key {Key}", key);
                return null;
            }
        }

        // Health check method
        public async Task<bool> IsHealthyAsync()
        {
            if (!_isRedisAvailable)
                return false;

            try
            {
                await _database.PingAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}