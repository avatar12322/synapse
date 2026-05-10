using System.Security.Claims;
using Synapse.Core.DTOs.Tenant;
using Synapse.Core.Services.Tenant;

namespace Synapse.Api.Endpoints.Tenant;

public static class TenantEndpoints
{
    private const string DefaultPrimary = "#7c3aed";
    private const string DefaultSecondary = "#1e293b";

    public static void MapTenantEndpoints(this WebApplication app)
    {
        // Public — frontend TenantProvider calls this on every app load
        app.MapGet("/api/tenant/branding", (ITenantContext tenantCtx) =>
        {
            if (!tenantCtx.HasTenant)
                return Results.Ok(new BrandingDto("Synapse", null, DefaultPrimary, DefaultSecondary));

            var t = tenantCtx.Current!;
            return Results.Ok(new BrandingDto(
                t.Name,
                t.LogoUrl,
                t.PrimaryColor ?? DefaultPrimary,
                t.SecondaryColor ?? DefaultSecondary));
        });

        var admin = app.MapGroup("/api/tenants").RequireAuthorization();

        admin.MapGet("/", async (ITenantService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!IsAdmin(user)) return Results.Forbid();
            return Results.Ok(await svc.ListAsync(ct));
        });

        admin.MapGet("/{id:int}", async (int id, ITenantService svc,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!IsAdmin(user)) return Results.Forbid();
            var t = await svc.GetByIdAsync(id, ct);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });

        admin.MapPost("/", async (CreateTenantRequest req, ITenantService svc,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!IsAdmin(user)) return Results.Forbid();
            var t = await svc.CreateAsync(req, ct);
            return Results.Created($"/api/tenants/{t.Id}", t);
        });

        admin.MapPut("/{id:int}", async (int id, UpdateTenantRequest req,
            ITenantService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!IsAdmin(user)) return Results.Forbid();
            var t = await svc.UpdateAsync(id, req, ct);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });
    }

    private static bool IsAdmin(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) == "Admin";
}
