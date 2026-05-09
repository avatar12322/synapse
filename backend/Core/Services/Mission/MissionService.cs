using Microsoft.EntityFrameworkCore;
using Synapse.Core.DTOs.Mission;
using Synapse.Core.Models.Mission;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Mission;

public interface IMissionService
{
    Task<MissionDto?> GetByIdAsync(int id, int requestingUserId, CancellationToken ct = default);
    Task<IEnumerable<MissionSummaryDto>> GetMyMissionsAsync(int userId, CancellationToken ct = default);
    Task<MissionDto?> AcceptAsync(int missionId, int userId, CancellationToken ct = default);
    Task<MissionDto?> VerifyCompletionAsync(string code, int businessOwnerId, CancellationToken ct = default);
    Task<MissionDto?> CancelAsync(int missionId, int userId, CancellationToken ct = default);
    Task ExpireOldMissionsAsync(CancellationToken ct = default);
}

public class MissionService(SynapseDbContext db) : IMissionService
{
    public async Task<MissionDto?> GetByIdAsync(int id, int requestingUserId, CancellationToken ct)
    {
        var m = await db.Missions
            .Include(x => x.Business)
            .Include(x => x.UserA)
            .Include(x => x.UserB)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (m is null) return null;

        // Only participants and business owner can see
        if (m.UserAId != requestingUserId && m.UserBId != requestingUserId
            && m.Business.OwnerId != requestingUserId)
            return null;

        return ToDto(m, requestingUserId);
    }

    public async Task<IEnumerable<MissionSummaryDto>> GetMyMissionsAsync(int userId, CancellationToken ct)
    {
        var missions = await db.Missions
            .Include(x => x.Business)
            .Where(x => (x.UserAId == userId || x.UserBId == userId)
                     && x.Status != MissionStatus.Expired
                     && x.Status != MissionStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return missions.Select(m => new MissionSummaryDto(
            m.Id, m.Title, m.Status.ToString(), m.Category.ToString(),
            m.Business.Name, m.DiscountPercent, m.ExpiresAt,
            IsMyTurn: (m.UserAId == userId && !m.UserAAccepted) || (m.UserBId == userId && !m.UserBAccepted)
        ));
    }

    public async Task<MissionDto?> AcceptAsync(int missionId, int userId, CancellationToken ct)
    {
        var m = await db.Missions.Include(x => x.Business)
            .FirstOrDefaultAsync(x => x.Id == missionId, ct);

        if (m is null || m.Status is MissionStatus.Cancelled or MissionStatus.Expired) return null;
        if (m.UserAId != userId && m.UserBId != userId) return null;

        if (m.UserAId == userId) m.UserAAccepted = true;
        if (m.UserBId == userId) m.UserBAccepted = true;

        if (m.UserAAccepted && m.UserBAccepted)
        {
            m.Status = MissionStatus.InProgress;
            m.LockedAt = DateTime.UtcNow;
            m.VerificationCode = GenerateCode();
            m.VerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(m.RequiredLockMinutes + 15);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(m, userId);
    }

    public async Task<MissionDto?> VerifyCompletionAsync(string code, int businessOwnerId, CancellationToken ct)
    {
        var m = await db.Missions
            .Include(x => x.Business)
            .FirstOrDefaultAsync(x =>
                x.VerificationCode == code.ToUpperInvariant()
                && x.Status == MissionStatus.InProgress
                && x.VerificationCodeExpiresAt > DateTime.UtcNow
                && x.Business.OwnerId == businessOwnerId, ct);

        if (m is null) return null;

        m.Status = MissionStatus.Completed;
        m.CompletedAt = DateTime.UtcNow;
        m.VerificationCode = null;

        await db.SaveChangesAsync(ct);
        return ToDto(m, businessOwnerId);
    }

    public async Task<MissionDto?> CancelAsync(int missionId, int userId, CancellationToken ct)
    {
        var m = await db.Missions.Include(x => x.Business)
            .FirstOrDefaultAsync(x => x.Id == missionId, ct);

        if (m is null) return null;
        if (m.UserAId != userId && m.UserBId != userId) return null;
        if (m.Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Expired) return null;

        m.Status = MissionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return ToDto(m, userId);
    }

    public async Task ExpireOldMissionsAsync(CancellationToken ct)
    {
        var expired = await db.Missions
            .Where(m => m.Status != MissionStatus.Completed
                     && m.Status != MissionStatus.Cancelled
                     && m.Status != MissionStatus.Expired
                     && m.ExpiresAt < DateTime.UtcNow)
            .Take(200)
            .ToListAsync(ct);

        foreach (var m in expired)
            m.Status = MissionStatus.Expired;

        if (expired.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static string GenerateCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    private static MissionDto ToDto(Models.Mission.Mission m, int requestingUserId)
    {
        // Only show verification code to participants, not business owner viewing it
        var code = m.Status == MissionStatus.InProgress ? m.VerificationCode : null;

        return new MissionDto(
            m.Id, m.Title, m.Description,
            m.Status.ToString(), m.Category.ToString(),
            m.BusinessId, m.Business.Name, m.Business.Address,
            m.Business.Location.Y, m.Business.Location.X,
            m.UserAId, m.UserBId,
            m.UserAAccepted, m.UserBAccepted,
            m.DiscountDescription, m.DiscountPercent,
            code,
            m.LockedAt, m.CompletedAt, m.RequiredLockMinutes,
            m.CreatedAt, m.ExpiresAt, m.InterestTags
        );
    }
}
