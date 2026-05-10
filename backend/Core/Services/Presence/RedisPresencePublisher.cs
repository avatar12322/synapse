using StackExchange.Redis;

namespace Synapse.Core.Services.Presence;

public class RedisPresencePublisher(IConnectionMultiplexer redis) : IPresencePublisher
{
    private static string StreamKey(int missionId) => $"presence:stream:{missionId}";

    public async Task PublishAsync(int missionId, string json, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StreamAddAsync(
            StreamKey(missionId),
            new[] { new NameValueEntry("json", json) },
            maxLength: 100,
            useApproximateMaxLength: true
        );
    }

    public async Task DeleteStreamAsync(int missionId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(StreamKey(missionId));
    }
}
