using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NetTopologySuite.Geometries;
using Synapse.Core.DTOs.Match;
using Synapse.Core.Models.Mission;
using Synapse.Infrastructure.Data;
using System.Text.Json;

namespace Synapse.Core.Services.Match;

public interface IMatchService
{
    Task<MatchResponse> RequestMatchAsync(int userId, MatchRequest req, CancellationToken ct = default);
}

public class MatchService(SynapseDbContext db, IDistributedCache cache, GeometryFactory gf) : IMatchService
{
    // Redis key prefix for the matching queue; value = JSON array of waiting user entries
    private const string QueueKeyPrefix = "match:queue:";

    public async Task<MatchResponse> RequestMatchAsync(int userId, MatchRequest req, CancellationToken ct)
    {
        // Find nearby active businesses
        var center = gf.CreatePoint(new Coordinate(req.Longitude, req.Latitude));
        center.SRID = 4326;

        var businessQuery = db.Businesses.Where(b =>
            b.IsActive && b.Location.IsWithinDistance(center, req.RadiusMetres));

        if (req.Category is not null)
            businessQuery = businessQuery.Where(b => b.Category == req.Category);

        var businesses = await businessQuery
            .OrderBy(b => b.Location.Distance(center))
            .Take(10)
            .ToListAsync(ct);

        if (businesses.Count == 0)
            return new MatchResponse("no_venues", null, "No active venues in range.");

        // Check if another user is already waiting (queue stored in Redis / distributed cache)
        var queueKey = QueueKey(req.Category ?? "any");
        var queueJson = await cache.GetStringAsync(queueKey, ct);
        var queue = queueJson is not null
            ? JsonSerializer.Deserialize<List<WaitingEntry>>(queueJson) ?? []
            : new List<WaitingEntry>();

        // Remove stale entries (> 5 min old)
        queue.RemoveAll(e => (DateTime.UtcNow - e.EnqueuedAt).TotalMinutes > 5);

        // Avoid self-match and users already in an active mission
        var existing = await db.Missions.AnyAsync(m =>
            (m.UserAId == userId || m.UserBId == userId) &&
            m.Status != MissionStatus.Expired &&
            m.Status != MissionStatus.Cancelled &&
            m.Status != MissionStatus.Completed, ct);

        if (existing)
            return new MatchResponse("searching", null, "You already have an active mission.");

        var partner = queue.FirstOrDefault(e => e.UserId != userId);

        if (partner is not null)
        {
            queue.Remove(partner);
            await SaveQueue(queueKey, queue, ct);

            // Pick best business (closest to midpoint between the two users)
            var partnerCenter = gf.CreatePoint(new Coordinate(partner.Longitude, partner.Latitude));
            partnerCenter.SRID = 4326;
            var midLat = (req.Latitude + partner.Latitude) / 2;
            var midLng = (req.Longitude + partner.Longitude) / 2;
            var midpoint = gf.CreatePoint(new Coordinate(midLng, midLat));
            midpoint.SRID = 4326;

            var bestBusiness = businesses
                .OrderBy(b => b.Location.Distance(midpoint))
                .First();

            var mission = new Models.Mission.Mission
            {
                Title = $"Coffee Break @ {bestBusiness.Name}",
                Description = "Meet your matched partner, lock your phones and enjoy 30 minutes together.",
                Category = req.Category is not null
                    ? Enum.TryParse<MissionCategory>(req.Category, true, out var cat) ? cat : MissionCategory.Coffee
                    : MissionCategory.Coffee,
                BusinessId = bestBusiness.Id,
                UserAId = userId,
                UserBId = partner.UserId,
                DiscountPercent = bestBusiness.DefaultDiscountPercent,
                DiscountDescription = $"{bestBusiness.DefaultDiscountPercent}% off your order",
                ExpiresAt = DateTime.UtcNow.AddHours(2)
            };

            db.Missions.Add(mission);
            await db.SaveChangesAsync(ct);

            return new MatchResponse("matched", mission.Id, null);
        }

        // No partner yet — enqueue this user
        queue.RemoveAll(e => e.UserId == userId);  // replace any stale own entry
        queue.Add(new WaitingEntry(userId, req.Latitude, req.Longitude, DateTime.UtcNow));
        await SaveQueue(queueKey, queue, ct);

        return new MatchResponse("searching", null, "Looking for a partner, you'll be notified.");
    }

    private async Task SaveQueue(string key, List<WaitingEntry> queue, CancellationToken ct)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        await cache.SetStringAsync(key, JsonSerializer.Serialize(queue), options, ct);
    }

    private static string QueueKey(string category) => $"{QueueKeyPrefix}{category.ToLowerInvariant()}";

    private record WaitingEntry(int UserId, double Latitude, double Longitude, DateTime EnqueuedAt);
}
