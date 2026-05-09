using System.Security.Claims;
using Synapse.Core.DTOs.Mission;
using Synapse.Core.Services.Mission;

namespace Synapse.Api.Endpoints.Mission;

public static class MissionEndpoints
{
    public static void MapMissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/missions").RequireAuthorization();

        // GET /api/missions — my active missions
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var missions = await svc.GetMyMissionsAsync(userId, ct);
            return Results.Ok(missions);
        });

        // GET /api/missions/{id}
        group.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var m = await svc.GetByIdAsync(id, userId, ct);
            return m is null ? Results.NotFound() : Results.Ok(m);
        });

        // POST /api/missions/{id}/accept
        group.MapPost("/{id:int}/accept", async (
            int id,
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var m = await svc.AcceptAsync(id, userId, ct);
            return m is null ? Results.BadRequest("Cannot accept this mission.") : Results.Ok(m);
        });

        // POST /api/missions/{id}/cancel
        group.MapPost("/{id:int}/cancel", async (
            int id,
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var m = await svc.CancelAsync(id, userId, ct);
            return m is null ? Results.BadRequest() : Results.Ok(m);
        });

        // POST /api/missions/verify — business staff enters 6-digit code
        group.MapPost("/verify", async (
            VerifyMissionRequest req,
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var businessOwnerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var m = await svc.VerifyCompletionAsync(req.Code, businessOwnerId, ct);
            return m is null
                ? Results.BadRequest(new { error = "Invalid or expired code, or minimum lock time not reached." })
                : Results.Ok(m);
        });

        // POST /api/missions/{id}/verify-nfc — participant submits NFC scan result for server-side HMAC verification
        group.MapPost("/{id:int}/verify-nfc", async (
            int id,
            NfcVerifyRequest req,
            ClaimsPrincipal user,
            IMissionService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (mission, error) = await svc.VerifyByNfcAsync(id, req.RawPayload, userId, ct);
            return mission is null
                ? Results.BadRequest(new { error })
                : Results.Ok(mission);
        });
    }
}

public record NfcVerifyRequest(string RawPayload);
