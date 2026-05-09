using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Synapse.Core.DTOs.Match;

namespace Synapse.Api.Endpoints.Presence;

/// <summary>
/// In-process WebSocket hub for mission presence updates.
/// Phase 3 will replace with Redis Streams / SignalR backplane.
/// </summary>
public static class PresenceEndpoints
{
    // missionId → set of connected sockets
    private static readonly ConcurrentDictionary<int, ConcurrentBag<WebSocket>> MissionSockets = new();

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

        // Auth: read JWT from cookie (same as REST endpoints)
        var user = ctx.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
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

                // Join mission room on first message
                if (joinedMission != update.MissionId)
                {
                    if (joinedMission.HasValue)
                        LeaveRoom(joinedMission.Value, ws);

                    joinedMission = update.MissionId;
                    MissionSockets.GetOrAdd(update.MissionId, _ => new ConcurrentBag<WebSocket>())
                                  .Add(ws);
                }

                // Broadcast to other participants in the same mission
                await BroadcastAsync(update.MissionId, json, ws, ctx.RequestAborted);
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

    private static async Task BroadcastAsync(int missionId, string json, WebSocket sender, CancellationToken ct)
    {
        if (!MissionSockets.TryGetValue(missionId, out var sockets)) return;

        var data = Encoding.UTF8.GetBytes(json);
        foreach (var socket in sockets)
        {
            if (socket == sender || socket.State != WebSocketState.Open) continue;
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, ct);
            }
            catch { /* disconnected peer */ }
        }
    }

    private static void LeaveRoom(int missionId, WebSocket ws)
    {
        if (!MissionSockets.TryGetValue(missionId, out var bag)) return;
        // ConcurrentBag doesn't support remove — rebuild without the disconnected socket
        var remaining = bag.Where(s => s != ws && s.State == WebSocketState.Open).ToList();
        if (remaining.Count == 0)
            MissionSockets.TryRemove(missionId, out _);
        else
        {
            var newBag = new ConcurrentBag<WebSocket>(remaining);
            MissionSockets[missionId] = newBag;
        }
    }
}
