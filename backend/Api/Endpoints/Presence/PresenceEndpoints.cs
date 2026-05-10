using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Synapse.Core.DTOs.Match;
using Synapse.Core.Services.Presence;

namespace Synapse.Api.Endpoints.Presence;

public static class PresenceEndpoints
{
    // missionId → set of locally connected sockets on this pod
    private static readonly ConcurrentDictionary<int, ConcurrentBag<WebSocket>> MissionSockets = new();

    // Missions that have at least one local socket — read by PresenceStreamConsumer
    internal static readonly ConcurrentDictionary<int, bool> ActiveMissionStreams = new();

    public static void MapPresenceEndpoints(this WebApplication app)
    {
        app.Map("/ws/presence", HandlePresenceAsync);
    }

    private static async Task HandlePresenceAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            return;
        }

        var user = ctx.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Resolve optional Redis publisher — if absent, fall back to in-process broadcast
        var publisher = ctx.RequestServices.GetService<IPresencePublisher>();
        var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        int? joinedMission = null;

        try
        {
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ctx.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                PresenceUpdateDto? update;
                try { update = JsonSerializer.Deserialize<PresenceUpdateDto>(json); }
                catch { continue; }

                if (update is null) continue;

                // Join mission room on first message or when switching missions
                if (joinedMission != update.MissionId)
                {
                    if (joinedMission.HasValue)
                        LeaveRoom(joinedMission.Value, ws);

                    joinedMission = update.MissionId;
                    MissionSockets.GetOrAdd(update.MissionId, _ => new ConcurrentBag<WebSocket>()).Add(ws);
                    ActiveMissionStreams.TryAdd(update.MissionId, true);
                }

                if (publisher is not null)
                    await publisher.PublishAsync(update.MissionId, json, ctx.RequestAborted);
                else
                    await BroadcastLocalAsync(update.MissionId, json, ctx.RequestAborted);
            }
        }
        finally
        {
            if (joinedMission.HasValue)
                LeaveRoom(joinedMission.Value, ws);

            if (ws.State != WebSocketState.Closed && ws.State != WebSocketState.Aborted)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", default);
        }
    }

    // Called by PresenceStreamConsumer to fan-out messages from Redis to local sockets.
    internal static async Task BroadcastLocalAsync(int missionId, string json, CancellationToken ct)
    {
        if (!MissionSockets.TryGetValue(missionId, out var sockets)) return;

        var data = Encoding.UTF8.GetBytes(json);
        foreach (var socket in sockets)
        {
            if (socket.State != WebSocketState.Open) continue;
            try { await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, ct); }
            catch { /* disconnected peer */ }
        }
    }

    private static void LeaveRoom(int missionId, WebSocket ws)
    {
        if (!MissionSockets.TryGetValue(missionId, out var bag)) return;
        var remaining = bag.Where(s => s != ws && s.State == WebSocketState.Open).ToList();
        if (remaining.Count == 0)
        {
            MissionSockets.TryRemove(missionId, out _);
            ActiveMissionStreams.TryRemove(missionId, out _);
        }
        else
            MissionSockets[missionId] = new ConcurrentBag<WebSocket>(remaining);
    }
}
