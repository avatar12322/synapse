using System.Security.Claims;
using Synapse.Core.Services.Reputation;

namespace Synapse.Api.Endpoints.Reputation;

public static class ReputationEndpoints
{
    public static void MapReputationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reputation").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IReputationService svc, CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var rep = await svc.GetReputationAsync(userId, ct);
            return Results.Ok(rep);
        });

        group.MapGet("/transactions", async (ClaimsPrincipal user, IReputationService svc, CancellationToken ct) =>
        {
            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var transactions = await svc.GetTransactionsAsync(userId, ct: ct);
            return Results.Ok(transactions);
        });
    }
}
