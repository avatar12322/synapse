using System.Security.Claims;
using Synapse.Core.DTOs.Business;
using Synapse.Core.Services.Business;

namespace Synapse.Api.Endpoints.Business;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/businesses");

        // Public: nearby businesses
        group.MapGet("/nearby", async (
            double lat, double lng, int radius = 2000, string? category = null,
            IBusinessService svc = default!, CancellationToken ct = default) =>
        {
            var results = await svc.GetNearbyAsync(lat, lng, radius, category, ct);
            return Results.Ok(results);
        });

        // Public: get single business
        group.MapGet("/{id:int}", async (int id, IBusinessService svc, CancellationToken ct) =>
        {
            var b = await svc.GetByIdAsync(id, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        });

        // Auth: create business (Role=Business)
        group.MapPost("/", async (
            CreateBusinessRequest req,
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.CreateAsync(ownerId, req, ct);
            return Results.Created($"/api/businesses/{b.Id}", b);
        }).RequireAuthorization();

        // Auth: update own business
        group.MapPut("/{id:int}", async (
            int id,
            UpdateBusinessRequest req,
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.UpdateAsync(id, ownerId, req, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).RequireAuthorization();

        // Auth: get my business
        group.MapGet("/mine", async (
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.GetByOwnerAsync(ownerId, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).RequireAuthorization();
    }
}
