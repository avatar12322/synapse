using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Synapse.Core.DTOs.Tenant;
using Synapse.Infrastructure.Data;
using System.Text.Json;
using TenantEntity = Synapse.Core.Models.Tenant.Tenant;

namespace Synapse.Core.Services.Tenant;

public interface ITenantService
{
    Task<TenantDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantEntity?> GetEntityBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantEntity?> GetEntityByDomainAsync(string domain, CancellationToken ct = default);
    Task<TenantDto> CreateAsync(CreateTenantRequest req, CancellationToken ct = default);
    Task<TenantDto?> UpdateAsync(int id, UpdateTenantRequest req, CancellationToken ct = default);
    Task<List<TenantDto>> ListAsync(CancellationToken ct = default);
}

public class TenantService(SynapseDbContext db, IDistributedCache cache) : ITenantService
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<TenantDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([id], ct);
        return tenant is null ? null : ToDto(tenant);
    }

    public async Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var entity = await GetEntityBySlugAsync(slug, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<TenantEntity?> GetEntityBySlugAsync(string slug, CancellationToken ct)
    {
        var cacheKey = $"tenant:slug:{slug}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<TenantEntity>(cached);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);

        if (tenant is not null)
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(tenant), CacheOptions, ct);

        return tenant;
    }

    public async Task<TenantEntity?> GetEntityByDomainAsync(string domain, CancellationToken ct)
    {
        var cacheKey = $"tenant:domain:{domain}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<TenantEntity>(cached);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.CustomDomain == domain && t.IsActive, ct);

        if (tenant is not null)
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(tenant), CacheOptions, ct);

        return tenant;
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest req, CancellationToken ct)
    {
        var tenant = new TenantEntity
        {
            Slug = req.Slug.ToLowerInvariant(),
            Name = req.Name,
            Country = req.Country.ToUpperInvariant(),
            VatRatePct = req.VatRatePct,
            LogoUrl = req.LogoUrl,
            PrimaryColor = req.PrimaryColor,
            SecondaryColor = req.SecondaryColor,
            CustomDomain = req.CustomDomain
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    public async Task<TenantDto?> UpdateAsync(int id, UpdateTenantRequest req, CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return null;

        if (req.Name is not null) tenant.Name = req.Name;
        if (req.LogoUrl is not null) tenant.LogoUrl = req.LogoUrl;
        if (req.PrimaryColor is not null) tenant.PrimaryColor = req.PrimaryColor;
        if (req.SecondaryColor is not null) tenant.SecondaryColor = req.SecondaryColor;
        if (req.CustomDomain is not null) tenant.CustomDomain = req.CustomDomain;
        if (req.VatRatePct.HasValue) tenant.VatRatePct = req.VatRatePct.Value;
        if (req.IsActive.HasValue) tenant.IsActive = req.IsActive.Value;

        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync($"tenant:slug:{tenant.Slug}", ct);
        if (tenant.CustomDomain is not null)
            await cache.RemoveAsync($"tenant:domain:{tenant.CustomDomain}", ct);

        return ToDto(tenant);
    }

    public async Task<List<TenantDto>> ListAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return tenants.Select(ToDto).ToList();
    }

    private static TenantDto ToDto(TenantEntity t) => new(
        t.Id, t.Slug, t.Name, t.LogoUrl,
        t.PrimaryColor, t.SecondaryColor, t.CustomDomain,
        t.Country, t.VatRatePct, t.IsActive, t.CreatedAt);
}
