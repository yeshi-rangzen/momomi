using MomomiAPI.Services.Interfaces;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MomomiAPI.Services.Implementations
{
    public class UpstashCacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ILogger<UpstashCacheService> _logger;
        private readonly bool _isRedisAvailable;
        private readonly SemaphoreSlim _semaphore = new(10, 10); // Allow 10 concurrent operations
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keySemaphores = new();

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
                return default(T);

            await _semaphore.WaitAsync();
            try
            {
                var value = await _database.StringGetAsync(key);
                if (!value.HasValue)
                    return default(T);

                var result = JsonSerializer.Deserialize<T>(value!);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache key {Key}", key);
                return default(T);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            if (!_isRedisAvailable)
                return;

            // Using _keySemaphores for preventing race conditions on specific keys
            var keySemaphore = _keySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await keySemaphore.WaitAsync();
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, serializedValue, expiry);
                _logger.LogDebug("Cache set for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key {Key}", key);
            }
            finally
            {
                keySemaphore.Release();

                // Optional: Clean up unused semaphores
                if (keySemaphore.CurrentCount == 1)
                {
                    _keySemaphores.TryRemove(key, out _);
                }
            }
        }

        // Set multiple key-value pairs with individual TTLs in a single operation
        public async Task SetManyAsync<T>(Dictionary<string, T> keyValuePairs, Dictionary<string, TimeSpan>? expiries = null)
        {
            if (!_isRedisAvailable || !keyValuePairs.Any())
            {
                return;
            }

            try
            {
                // OPTION 1: Pipeline approach (most efficient for mixed TTLs)
                await SetManyWithPipeline(keyValuePairs, expiries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting multiple cache keys");
                // Fallback to individual operations
                await FallbackToIndividualSets(keyValuePairs, expiries);
            }
        }

        // Get multiple keys in a single operation
        public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys)
        {
            var result = new Dictionary<string, T?>();
            var keyList = keys.ToList();

            if (!_isRedisAvailable || !keyList.Any())
            {
                foreach (var key in keyList)
                {
                    result[key] = default(T);
                }

                return result;
            }

            try
            {
                // Use MGET for batch retrieval
                var redisKeys = keyList.Select(k => (RedisKey)k).ToArray();
                var values = await _database.StringGetAsync(redisKeys);

                for (int i = 0; i < keyList.Count; i++)
                {
                    var key = keyList[i];
                    var value = values[i];

                    if (value.HasValue)
                    {
                        try
                        {
                            result[key] = JsonSerializer.Deserialize<T>(value!);
                            _logger.LogDebug("Batch cache hit for key: {Key}", key);
                        }
                        catch (JsonException jsonEx)
                        {
                            _logger.LogError(jsonEx, "Error deserializing cache value for key {Key}", key);
                            result[key] = default(T);
                        }
                    }
                    else
                    {
                        result[key] = default(T);
                        _logger.LogDebug("Batch cache miss for key: {Key}", key);
                    }
                }

                _logger.LogInformation("Batch get completed for {Count} keys", keyList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple cache keys");
                // Fallback to individual gets
                return await FallbackToIndividualGets<T>(keyList);
            }
        }

        // Remove multiple keys in a single operation
        public async Task RemoveManyAsync(IEnumerable<string> keys)
        {
            var keyList = keys.ToList();
            if (!_isRedisAvailable || !keyList.Any())
            {
                _logger.LogWarning("Redis not available or no keys provided for removal");
                return;
            }

            try
            {
                var redisKeys = keyList.Select(k => (RedisKey)k).ToArray();
                var deletedCount = await _database.KeyDeleteAsync(redisKeys);

                _logger.LogInformation("Batch removed {DeletedCount}/{TotalCount} cache keys", deletedCount, keyList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cache keys");
            }
        }

        // Cache-Aside Pattern with Locking
        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            if (!_isRedisAvailable)
            {
                _logger.LogWarning("Redis not available, executing factory directly for key: {Key}", key);
                return await factory();
            }

            // Try to get from cache first
            var cached = await GetAsync<T>(key);
            if (cached != null) return cached;

            // Use per-key semaphore to prevent cache stampede
            var keySemaphore = _keySemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await keySemaphore.WaitAsync();
            try
            {
                // Double-check pattern
                cached = await GetAsync<T>(key);
                if (cached != null) return cached;

                // Execute factory and cache result
                var value = await factory();
                if (value != null)
                {
                    await SetAsync(key, value, expiry);
                }
                return value;
            }
            finally
            {
                keySemaphore.Release();

                // Clean up semaphore if no one is waiting
                if (keySemaphore.CurrentCount == 1)
                {
                    _keySemaphores.TryRemove(key, out _);
                    keySemaphore.Dispose();
                }
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key {Key}", key);
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

        #region Private Helper Methods
        // Pipleline approach for mixed TTLs
        private async Task SetManyWithPipeline<T>(Dictionary<string, T> keyValuePairs, Dictionary<string, TimeSpan>? expiries)
        {
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var kvp in keyValuePairs)
            {
                var serializedValue = JsonSerializer.Serialize(kvp.Value);
                var expiry = expiries?.GetValueOrDefault(kvp.Key);

                // Add SET operation to batch
                tasks.Add(batch.StringSetAsync(kvp.Key, serializedValue, expiry));
            }

            // Execute all operations in parallel
            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation("Pipeline batch set completed for {Count} keys", keyValuePairs.Count);
        }

        // Fallback to individual SET operations
        private async Task FallbackToIndividualSets<T>(Dictionary<string, T> keyValuePairs, Dictionary<string, TimeSpan>? expiries)
        {
            _logger.LogWarning("Falling back to individual SET operations for {Count} keys", keyValuePairs.Count);

            var tasks = keyValuePairs.Select(async kvp =>
            {
                var expiry = expiries?.GetValueOrDefault(kvp.Key);
                await SetAsync(kvp.Key, kvp.Value, expiry);
            });

            await Task.WhenAll(tasks);
        }

        /// Fallback to individual GET operations if batch fails
        private async Task<Dictionary<string, T?>> FallbackToIndividualGets<T>(List<string> keys)
        {
            _logger.LogWarning("Falling back to individual GET operations for {Count} keys", keys.Count);

            var result = new Dictionary<string, T?>();
            var tasks = keys.Select(async key =>
            {
                var value = await GetAsync<T>(key);
                return new { Key = key, Value = value };
            });

            var results = await Task.WhenAll(tasks);
            foreach (var item in results)
            {
                result[item.Key] = item.Value;
            }

            return result;
        }
        #endregion
    }
}