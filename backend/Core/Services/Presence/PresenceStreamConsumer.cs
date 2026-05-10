using System.Collections.Concurrent;
using StackExchange.Redis;
using Synapse.Api.Endpoints.Presence;

namespace Synapse.Core.Services.Presence;

// Polls Redis Streams for each locally-active mission and fans out to connected WebSockets.
// Each pod instance tracks its own read position per stream — pure fan-out, no consumer groups.
public class PresenceStreamConsumer(IConnectionMultiplexer redis, ILogger<PresenceStreamConsumer> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<int, RedisValue> _lastIds = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                foreach (var missionId in PresenceEndpoints.ActiveMissionStreams.Keys.ToList())
                {
                    var streamKey = $"presence:stream:{missionId}";
                    var position = _lastIds.GetOrAdd(missionId, "0-0");

                    StreamEntry[] messages;
                    try { messages = await db.StreamReadAsync(streamKey, position, count: 50); }
                    catch { continue; }

                    if (messages.Length == 0) continue;

                    foreach (var msg in messages)
                    {
                        string? json = null;
                        foreach (var field in msg.Values)
                            if (field.Name == "json") { json = (string?)field.Value; break; }

                        if (json is not null)
                            await PresenceEndpoints.BroadcastLocalAsync(missionId, json, ct);
                    }

                    _lastIds[missionId] = messages[^1].Id;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Presence stream consumer error");
            }

            await Task.Delay(100, ct);
        }
    }
}
