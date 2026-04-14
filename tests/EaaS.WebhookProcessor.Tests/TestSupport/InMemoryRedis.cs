using NSubstitute;
using StackExchange.Redis;

namespace EaaS.WebhookProcessor.Tests.TestSupport;

/// <summary>
/// Minimal dictionary-backed fake IConnectionMultiplexer for Redis dedup tests.
/// Only StringSetAsync(key, value, ttl, When.NotExists) is wired — the SNS handlers call
/// nothing else on IDatabase in this scope.
/// </summary>
internal static class InMemoryRedis
{
    public static IConnectionMultiplexer Build()
    {
        var store = new Dictionary<string, object>(StringComparer.Ordinal);
        var db = Substitute.For<IDatabase>();
        db.StringSetAsync(
                default(RedisKey),
                default(RedisValue),
                default(TimeSpan?),
                default(When))
            .ReturnsForAnyArgs(call =>
            {
                var key = ((RedisKey)call[0]).ToString();
                var value = (RedisValue)call[1];
                var when = (When)call[3];

                if (when == When.NotExists)
                {
                    if (store.ContainsKey(key))
                    {
                        return Task.FromResult(false);
                    }
                    store[key] = value;
                    return Task.FromResult(true);
                }

                store[key] = value;
                return Task.FromResult(true);
            });

        var mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        return mux;
    }
}
