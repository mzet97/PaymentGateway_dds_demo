using System.Collections.Concurrent;
using System.Net;
using Moq;
using StackExchange.Redis;

namespace PaymentGateway.IntegrationTests;

public static class TestConnectionMultiplexerFactory
{
    public static IConnectionMultiplexer Create()
    {
        var stringStore = new ConcurrentDictionary<string, RedisValue>();
        var sortedSetStore = new ConcurrentDictionary<string, SortedSet<(double Score, string Member)>>();

        var dbMock = new Mock<IDatabase>(MockBehavior.Loose);

        // === String operations (for IdempotencyMiddleware) ===
        dbMock.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) =>
                Task.FromResult(stringStore.TryGetValue(key.ToString(), out var value) ? value : RedisValue.Null));

        // StringSetAsync (4 params - key, value, expiry, when)
        dbMock.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? _, When _) =>
            {
                stringStore[key.ToString()] = value;
                return Task.FromResult(true);
            });

        // StringSetAsync (5 params)
        dbMock.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? _, When _, CommandFlags _) =>
            {
                stringStore[key.ToString()] = value;
                return Task.FromResult(true);
            });

        // StringSetAsync (6 params with keepTtl)
        dbMock.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? _, bool _, When _, CommandFlags _) =>
            {
                stringStore[key.ToString()] = value;
                return Task.FromResult(true);
            });

        // === Sorted set operations (for RateLimitingMiddleware) ===
        dbMock.Setup(db => db.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        dbMock.Setup(db => db.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        dbMock.Setup(db => db.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        dbMock.Setup(db => db.SortedSetAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<double>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        dbMock.Setup(db => db.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        dbMock.Setup(db => db.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // === SortedSetRangeByScoreAsync (for CalculateRetryAfterAsync) ===
        dbMock.Setup(db => db.SortedSetRangeByScoreAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<Order>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        // === Connection multiplexer ===
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);
        multiplexerMock.Setup(m => m.IsConnected).Returns(true);
        multiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { new IPEndPoint(IPAddress.Loopback, 6379) });

        return multiplexerMock.Object;
    }
}
