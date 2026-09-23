using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Services.JwtBlacklistService;
using Services.Utils;

namespace SportsCenterManagement.Tests;

public class JwtBlacklistServiceTests
{
    [Fact]
    public async Task AddTokenToBlacklistAsync_StoresJtiUntilTokenExpiration()
    {
        using var cache = new RecordingMemoryCache();
        var service = new JwtBlacklistService(cache);
        var token = AccessTokenUtilTests.CreateToken("logout-jti", DateTime.UtcNow.AddMinutes(30));
        AccessTokenUtil.TryReadActiveToken(token, out var tokenInfo);

        var added = await service.AddTokenToBlacklistAsync(token);

        Assert.True(added);
        Assert.Equal("jwt:blacklist:logout-jti", cache.LastKey);
        Assert.Equal(tokenInfo!.ExpiresAtUtc, cache.LastAbsoluteExpiration!.Value.UtcDateTime);
        Assert.True(await service.IsTokenBlacklistedAsync(token));
    }

    [Fact]
    public async Task AddTokenToBlacklistAsync_RejectsMalformedToken()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new JwtBlacklistService(cache);

        Assert.False(await service.AddTokenToBlacklistAsync("not-a-jwt"));
    }

    private sealed class RecordingMemoryCache : IMemoryCache
    {
        private readonly Dictionary<object, object?> _values = new();

        public object? LastKey { get; private set; }

        public DateTimeOffset? LastAbsoluteExpiration { get; private set; }

        public ICacheEntry CreateEntry(object key)
        {
            LastKey = key;
            return new RecordingCacheEntry(key, entry =>
            {
                _values[entry.Key] = entry.Value;
                LastAbsoluteExpiration = entry.AbsoluteExpiration;
            });
        }

        public bool TryGetValue(object key, out object? value) => _values.TryGetValue(key, out value);

        public void Remove(object key) => _values.Remove(key);

        public void Dispose()
        {
        }

        private sealed class RecordingCacheEntry : ICacheEntry
        {
            private readonly Action<RecordingCacheEntry> _onDispose;

            public RecordingCacheEntry(object key, Action<RecordingCacheEntry> onDispose)
            {
                Key = key;
                _onDispose = onDispose;
            }

            public object Key { get; }
            public object? Value { get; set; }
            public DateTimeOffset? AbsoluteExpiration { get; set; }
            public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
            public TimeSpan? SlidingExpiration { get; set; }
            public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();
            public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();
            public CacheItemPriority Priority { get; set; }
            public long? Size { get; set; }

            public void Dispose() => _onDispose(this);
        }
    }
}
