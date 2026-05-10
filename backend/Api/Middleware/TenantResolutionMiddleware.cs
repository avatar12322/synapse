using Synapse.Core.Services.Tenant;
using TenantEntity = Synapse.Core.Models.Tenant.Tenant;

namespace Synapse.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantService tenantSvc, ITenantContext tenantCtx)
    {
        var resolved = await ResolveAsync(ctx, tenantSvc);
        ((TenantContext)tenantCtx).Current = resolved;
        await next(ctx);
    }

    private static async Task<TenantEntity?> ResolveAsync(
        HttpContext ctx, ITenantService tenantSvc)
    {
        var ct = ctx.RequestAborted;

        // 1. Explicit header — highest priority (used by mobile clients)
        var slug = ctx.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(slug))
            return await tenantSvc.GetEntityBySlugAsync(slug, ct);

        // 2. Subdomain: "acme.synapse.app" → slug "acme"
        var host = ctx.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            var subdomainSlug = parts[0];
            var bySlug = await tenantSvc.GetEntityBySlugAsync(subdomainSlug, ct);
            if (bySlug is not null) return bySlug;
        }

        // 3. Custom domain exact match (e.g. "missions.coworking.pl")
        return await tenantSvc.GetEntityByDomainAsync(host, ct);
    }
}
