namespace MomomiAPI.Services.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task SetManyAsync<T>(Dictionary<string, T> keyValuePairs, Dictionary<string, TimeSpan>? expiries = null);
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);

        Task RemoveAsync(string key);
        Task RemoveManyAsync(IEnumerable<string> keys);
        Task<bool> ExistsAsync(string key);
        Task SetStringAsync(string key, string value, TimeSpan? expiry = null);
        Task<string?> GetStringAsync(string key);
    }
}
