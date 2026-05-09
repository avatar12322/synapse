using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Synapse.Core.DTOs.Business;
using Synapse.Core.Models.Business;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Business;

public interface IBusinessService
{
    Task<BusinessDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<BusinessDto>> GetNearbyAsync(double lat, double lng, int radiusMetres, string? category, CancellationToken ct = default);
    Task<BusinessDto> CreateAsync(int ownerId, CreateBusinessRequest req, CancellationToken ct = default);
    Task<BusinessDto?> UpdateAsync(int id, int ownerId, UpdateBusinessRequest req, CancellationToken ct = default);
    Task<BusinessDto?> GetByOwnerAsync(int ownerId, CancellationToken ct = default);
}

public class BusinessService(SynapseDbContext db, GeometryFactory gf) : IBusinessService
{
    public async Task<BusinessDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var b = await db.Businesses.Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return b is null ? null : ToDto(b, null);
    }

    public async Task<IEnumerable<BusinessDto>> GetNearbyAsync(
        double lat, double lng, int radiusMetres, string? category, CancellationToken ct)
    {
        var center = gf.CreatePoint(new Coordinate(lng, lat));
        center.SRID = 4326;

        var query = db.Businesses
            .Where(b => b.IsActive && b.Location.IsWithinDistance(center, radiusMetres));

        if (!string.IsNullOrEmpty(category))
            query = query.Where(b => b.Category == category);

        var results = await query
            .OrderBy(b => b.Location.Distance(center))
            .Take(50)
            .ToListAsync(ct);

        return results.Select(b => ToDto(b, b.Location.Distance(center)));
    }

    public async Task<BusinessDto> CreateAsync(int ownerId, CreateBusinessRequest req, CancellationToken ct)
    {
        var point = gf.CreatePoint(new Coordinate(req.Longitude, req.Latitude));
        point.SRID = 4326;

        var business = new Models.Business.Business
        {
            Name = req.Name,
            Address = req.Address,
            City = req.City,
            Location = point,
            Category = req.Category,
            Description = req.Description,
            DefaultDiscountPercent = req.DefaultDiscountPercent,
            OwnerId = ownerId
        };

        db.Businesses.Add(business);
        await db.SaveChangesAsync(ct);
        return ToDto(business, null);
    }

    public async Task<BusinessDto?> UpdateAsync(int id, int ownerId, UpdateBusinessRequest req, CancellationToken ct)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, ct);
        if (business is null) return null;

        if (req.Name is not null) business.Name = req.Name;
        if (req.Address is not null) business.Address = req.Address;
        if (req.City is not null) business.City = req.City;
        if (req.Category is not null) business.Category = req.Category;
        if (req.Description is not null) business.Description = req.Description;
        if (req.IsActive is not null) business.IsActive = req.IsActive.Value;
        if (req.DefaultDiscountPercent is not null) business.DefaultDiscountPercent = req.DefaultDiscountPercent.Value;

        if (req.Latitude is not null && req.Longitude is not null)
        {
            var point = gf.CreatePoint(new Coordinate(req.Longitude.Value, req.Latitude.Value));
            point.SRID = 4326;
            business.Location = point;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(business, null);
    }

    public async Task<BusinessDto?> GetByOwnerAsync(int ownerId, CancellationToken ct)
    {
        var b = await db.Businesses.FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct);
        return b is null ? null : ToDto(b, null);
    }

    private static BusinessDto ToDto(Models.Business.Business b, double? distanceDeg)
    {
        // distanceDeg is in coordinate units (degrees) for geometry type; geography uses metres
        // With geography(Point, 4326) the distance is already in metres
        return new BusinessDto(
            b.Id, b.Name, b.Address, b.City,
            b.Location.Y, b.Location.X,
            b.Category, b.Description,
            b.IsActive, b.DefaultDiscountPercent,
            b.StripeOnboardingComplete,
            distanceDeg
        );
    }
}
