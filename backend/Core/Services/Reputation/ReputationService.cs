using Microsoft.EntityFrameworkCore;
using Synapse.Core.DTOs.Reputation;
using Synapse.Core.Models.User;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Reputation;

public interface IReputationService
{
    Task AwardPointsAsync(int userId, int points, ReputationReason reason, int? missionId = null, CancellationToken ct = default);
    Task<UserReputationDto?> GetReputationAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<ReputationTransactionDto>> GetTransactionsAsync(int userId, int limit = 50, CancellationToken ct = default);
}

public class ReputationService(SynapseDbContext db) : IReputationService
{
    public async Task AwardPointsAsync(int userId, int points, ReputationReason reason, int? missionId = null, CancellationToken ct = default)
    {
        var rep = await db.UserReputations
            .Include(r => r.Transactions)
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null)
        {
            rep = new UserReputation { UserId = userId };
            db.UserReputations.Add(rep);
            await db.SaveChangesAsync(ct);
        }

        rep.TotalPoints += points;
        rep.RepLevel = UserReputation.CalculateLevel(rep.TotalPoints);
        rep.UpdatedAt = DateTime.UtcNow;

        db.ReputationTransactions.Add(new ReputationTransaction
        {
            UserReputationId = rep.Id,
            Points = points,
            Reason = reason,
            MissionId = missionId,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<UserReputationDto?> GetReputationAsync(int userId, CancellationToken ct = default)
    {
        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null)
            return new UserReputationDto(userId, 0, 1, 100, 0.0, DateTime.UtcNow);

        var nextThreshold = UserReputation.NextLevelThreshold(rep.RepLevel);
        var prevThreshold = UserReputation.NextLevelThreshold(rep.RepLevel - 1);
        var toNext = nextThreshold == 0 ? 0 : nextThreshold - rep.TotalPoints;
        double progress = nextThreshold == 0
            ? 100.0
            : Math.Round((double)(rep.TotalPoints - prevThreshold) / (nextThreshold - prevThreshold) * 100, 1);

        return new UserReputationDto(userId, rep.TotalPoints, rep.RepLevel, toNext, progress, rep.UpdatedAt);
    }

    public async Task<IEnumerable<ReputationTransactionDto>> GetTransactionsAsync(int userId, int limit = 50, CancellationToken ct = default)
    {
        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null) return [];

        return await db.ReputationTransactions
            .AsNoTracking()
            .Where(t => t.UserReputationId == rep.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new ReputationTransactionDto(t.Id, t.Points, t.Reason.ToString(), t.MissionId, t.CreatedAt))
            .ToListAsync(ct);
    }
}
