using H3;
using H3.Algorithms;
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
    public async Task<MatchResponse> RequestMatchAsync(int userId, MatchRequest req, CancellationToken ct)
    {
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

        var existing = await db.Missions.AnyAsync(m =>
            (m.UserAId == userId || m.UserBId == userId) &&
            m.Status != MissionStatus.Expired &&
            m.Status != MissionStatus.Cancelled &&
            m.Status != MissionStatus.Completed, ct);

        if (existing)
            return new MatchResponse("searching", null, "You already have an active mission.");

        // Geosharding: queue key scoped to H3 res-6 cell (~36 km²)
        var userCell = H3Index.FromPoint(center, 6);
        var category = req.Category ?? "any";

        // Search own cell first, then immediate neighbors to handle cell boundary crossings
        var (partner, partnerQueueKey) = await FindPartnerAsync(userCell, category, userId, ct);
        if (partner is null)
        {
            foreach (var ring in userCell.GridDiskDistances(1))
            {
                if (ring.Index == userCell) continue;
                (partner, partnerQueueKey) = await FindPartnerAsync(ring.Index, category, userId, ct);
                if (partner is not null) break;
            }
        }

        if (partner is not null)
        {
            // Remove matched partner from their shard queue
            var pQueueKey = QueueKey(ring: null, cell: partnerQueueKey.Split(':')[2], category);
            await RemoveFromQueue(partnerQueueKey, partner.UserId, ct);

            var midpoint = gf.CreatePoint(new Coordinate(
                (req.Longitude + partner.Longitude) / 2,
                (req.Latitude + partner.Latitude) / 2));
            midpoint.SRID = 4326;

            var bestBusiness = businesses.OrderBy(b => b.Location.Distance(midpoint)).First();

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
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                H3Index = bestBusiness.H3Index
                    ?? H3Index.FromPoint(bestBusiness.Location, 6).ToString()
            };

            db.Missions.Add(mission);
            await db.SaveChangesAsync(ct);

            return new MatchResponse("matched", mission.Id, null);
        }

        // No partner — add self to this shard's queue
        var ownKey = QueueKey(userCell, category);
        var ownJson = await cache.GetStringAsync(ownKey, ct);
        var ownQueue = ownJson is not null
            ? JsonSerializer.Deserialize<List<WaitingEntry>>(ownJson) ?? []
            : new List<WaitingEntry>();

        ownQueue.RemoveAll(e => e.UserId == userId);
        ownQueue.Add(new WaitingEntry(userId, req.Latitude, req.Longitude, userCell.ToString(), DateTime.UtcNow));
        await SaveQueue(ownKey, ownQueue, ct);

        // GDPR: record approximate user location at res 9 for later pseudonymization
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            profile.LastKnownH3 = H3Index.FromPoint(center, 9).ToString();
            profile.LastKnownH3CapturedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return new MatchResponse("searching", null, "Looking for a partner, you'll be notified.");
    }

    private async Task<(WaitingEntry? partner, string queueKey)> FindPartnerAsync(
        H3Index cell, string category, int userId, CancellationToken ct)
    {
        var key = QueueKey(cell, category);
        var json = await cache.GetStringAsync(key, ct);
        if (json is null) return (null, key);

        var queue = JsonSerializer.Deserialize<List<WaitingEntry>>(json) ?? [];
        queue.RemoveAll(e => (DateTime.UtcNow - e.EnqueuedAt).TotalMinutes > 5);

        var partner = queue.FirstOrDefault(e => e.UserId != userId);
        return (partner, key);
    }

    private async Task RemoveFromQueue(string queueKey, int userId, CancellationToken ct)
    {
        var json = await cache.GetStringAsync(queueKey, ct);
        if (json is null) return;
        var queue = JsonSerializer.Deserialize<List<WaitingEntry>>(json) ?? [];
        queue.RemoveAll(e => e.UserId == userId);
        await SaveQueue(queueKey, queue, ct);
    }

    private async Task SaveQueue(string key, List<WaitingEntry> queue, CancellationToken ct)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };
        await cache.SetStringAsync(key, JsonSerializer.Serialize(queue), options, ct);
    }

    private static string QueueKey(H3Index cell, string category)
        => $"match:queue:{cell}:{category.ToLowerInvariant()}";

    // Overload for cases where cell string is pre-extracted
    private static string QueueKey(object? ring, string cell, string category)
        => $"match:queue:{cell}:{category.ToLowerInvariant()}";

    private record WaitingEntry(int UserId, double Latitude, double Longitude, string H3Cell, DateTime EnqueuedAt);
}
