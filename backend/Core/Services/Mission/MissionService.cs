using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Synapse.Core.DTOs.Mission;
using Synapse.Core.Models.Mission;
using Synapse.Core.Models.User;
using Synapse.Core.Services.Reputation;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Mission;

public interface IMissionService
{
    Task<MissionDto?> GetByIdAsync(int id, int requestingUserId, CancellationToken ct = default);
    Task<IEnumerable<MissionSummaryDto>> GetMyMissionsAsync(int userId, CancellationToken ct = default);
    Task<MissionDto?> AcceptAsync(int missionId, int userId, CancellationToken ct = default);
    Task<MissionDto?> VerifyCompletionAsync(string code, int businessOwnerId, CancellationToken ct = default);
    Task<(MissionDto? Mission, string? Error)> VerifyByNfcAsync(int missionId, string rawPayload, int userId, CancellationToken ct = default);
    Task<MissionDto?> CancelAsync(int missionId, int userId, CancellationToken ct = default);
    Task ExpireOldMissionsAsync(CancellationToken ct = default);
}

public class MissionService(SynapseDbContext db, IReputationService reputationService) : IMissionService
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

        // Award reputation points to both participants
        if (m.UserAId.HasValue)
            await reputationService.AwardPointsAsync(m.UserAId.Value, 50, ReputationReason.MissionCompleted, m.Id, ct);
        if (m.UserBId.HasValue)
            await reputationService.AwardPointsAsync(m.UserBId.Value, 50, ReputationReason.MissionCompleted, m.Id, ct);

        return ToDto(m, businessOwnerId);
    }

    public async Task<(MissionDto? Mission, string? Error)> VerifyByNfcAsync(
        int missionId, string rawPayload, int userId, CancellationToken ct)
    {
        // Parse NDEF payload: { "v":1, "bid":"...", "mid":"...", "ts":epoch, "sig":"hex" }
        JsonElement payload;
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            payload = doc.RootElement.Clone();
        }
        catch
        {
            return (null, "Invalid JSON payload");
        }

        if (!payload.TryGetProperty("bid", out var bidEl) ||
            !payload.TryGetProperty("mid", out var midEl) ||
            !payload.TryGetProperty("ts", out var tsEl) ||
            !payload.TryGetProperty("sig", out var sigEl))
            return (null, "Payload missing required fields (bid, mid, ts, sig)");

        var bid = bidEl.GetString() ?? string.Empty;
        var mid = midEl.GetString() ?? string.Empty;
        var sig = sigEl.GetString() ?? string.Empty;
        var ts = tsEl.GetInt64();

        // Replay protection: tag must be no older than 5 minutes
        var tagTime = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
        if (Math.Abs((DateTime.UtcNow - tagTime).TotalMinutes) > 5)
            return (null, "Tag timestamp expired (replay protection)");

        if (!int.TryParse(mid, out var tagMissionId) || tagMissionId != missionId)
            return (null, "Payload missionId does not match requested mission");

        var m = await db.Missions
            .Include(x => x.Business)
            .FirstOrDefaultAsync(x => x.Id == missionId, ct);

        if (m is null) return (null, "Mission not found");
        if (m.Status != MissionStatus.InProgress) return (null, "Mission is not in progress");
        if (m.UserAId != userId && m.UserBId != userId) return (null, "Not a participant of this mission");
        if (m.Business.Id.ToString() != bid) return (null, "Tag businessId does not match mission business");

        // Verify HMAC-SHA256: key = NfcSecret (base64), message = "v=1|bid=<bid>|mid=<mid>|ts=<ts>"
        // DEV BYPASS: allow "mock_sig" in development
        if (sig != "mock_sig")
        {
            if (string.IsNullOrEmpty(m.Business.NfcSecret))
                return (null, "NFC not configured for this business");

            var message = $"v=1|bid={bid}|mid={mid}|ts={ts}";
            var keyBytes = Convert.FromBase64String(m.Business.NfcSecret);
            var msgBytes = Encoding.UTF8.GetBytes(message);
            var expectedSig = Convert.ToHexString(HMACSHA256.HashData(keyBytes, msgBytes)).ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedSig),
                Encoding.ASCII.GetBytes(sig.ToLowerInvariant())))
                return (null, "Invalid NFC tag signature");
        }

        m.Status = MissionStatus.Completed;
        m.CompletedAt = DateTime.UtcNow;
        m.VerificationCode = null;
        m.VerificationCodeExpiresAt = null;

        await db.SaveChangesAsync(ct);

        // Award reputation points for NFC-verified completion
        if (m.UserAId.HasValue)
            await reputationService.AwardPointsAsync(m.UserAId.Value, 50, ReputationReason.NfcVerification, m.Id, ct);
        if (m.UserBId.HasValue)
            await reputationService.AwardPointsAsync(m.UserBId.Value, 50, ReputationReason.NfcVerification, m.Id, ct);

        return (ToDto(m, userId), null);
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
