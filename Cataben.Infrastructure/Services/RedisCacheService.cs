using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Cataben.Infrastructure.Services
{
    public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        : ICacheService
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly IDatabase _database = redis.GetDatabase();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                if (value.IsNullOrEmpty)
                    return default;

                return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting cache for key {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                if (expiration.HasValue)
                    await _database.StringSetAsync(key, json, expiration.Value);
                else
                    await _database.StringSetAsync(key, json);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting cache for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error removing cache for key {Key}", key);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking cache for key {Key}", key);
                return false;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
                return cached;

            var value = await factory();
            await SetAsync(key, value, expiration);
            return value;
        }
    }
}
