using System.Security.Claims;
using Synapse.Core.DTOs.Match;
using Synapse.Core.Services.Match;

namespace Synapse.Api.Endpoints.Match;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        // POST /api/match — request a match with location + optional category preference
        app.MapPost("/api/match", async (
            MatchRequest req,
            ClaimsPrincipal user,
            IMatchService svc,
            CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await svc.RequestMatchAsync(userId, req, ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
