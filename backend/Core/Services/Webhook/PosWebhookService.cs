using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Synapse.Core.Models.Mission;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Webhook;

public interface IPosWebhookService
{
    /// <summary>Verifies HMAC signature and processes a POS sale event.</summary>
    Task<(bool Success, string? MissionId, string? Error)> ProcessAsync(
        string businessId, string rawBody, string signature, CancellationToken ct = default);

    Task<string> GeneratePosSecretAsync(int businessId, CancellationToken ct = default);
    Task<string> GenerateNfcSecretAsync(int businessId, CancellationToken ct = default);
}

public class PosWebhookService(SynapseDbContext db) : IPosWebhookService
{
    public async Task<(bool Success, string? MissionId, string? Error)> ProcessAsync(
        string businessId, string rawBody, string signature, CancellationToken ct)
    {
        if (!int.TryParse(businessId, out var bizId))
            return (false, null, "Invalid businessId format");

        var business = await db.Businesses.FindAsync([bizId], ct);
        if (business is null)
            return (false, null, "Business not found");

        if (string.IsNullOrEmpty(business.PosWebhookSecret))
            return (false, null, "POS webhook not configured for this business");

        if (!VerifyHmac(rawBody, signature, business.PosWebhookSecret))
            return (false, null, "Invalid signature");

        // Parse verification code from body (already deserialized by caller)
        // We re-parse here to keep the service self-contained
        var payload = System.Text.Json.JsonSerializer.Deserialize<PosPayloadInternal>(rawBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload is null || string.IsNullOrEmpty(payload.VerificationCode))
            return (false, null, "Missing verificationCode in payload");

        var code = payload.VerificationCode.ToUpperInvariant();
        var now = DateTime.UtcNow;

        var mission = await db.Missions
            .Where(m =>
                m.BusinessId == bizId &&
                m.VerificationCode == code &&
                m.Status == MissionStatus.InProgress &&
                m.VerificationCodeExpiresAt > now)
            .FirstOrDefaultAsync(ct);

        if (mission is null)
            return (false, null, "No matching active mission found for this code");

        mission.Status = MissionStatus.Completed;
        mission.CompletedAt = now;
        mission.VerifiedByPos = true;
        mission.PosTransactionId = payload.TransactionId;
        mission.VerificationCode = null;
        mission.VerificationCodeExpiresAt = null;

        await db.SaveChangesAsync(ct);
        return (true, mission.Id.ToString(), null);
    }

    public async Task<string> GeneratePosSecretAsync(int businessId, CancellationToken ct)
    {
        var business = await db.Businesses.FindAsync([businessId], ct)
            ?? throw new KeyNotFoundException($"Business {businessId} not found");

        var secret = GenerateSecret();
        business.PosWebhookSecret = secret;
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task<string> GenerateNfcSecretAsync(int businessId, CancellationToken ct)
    {
        var business = await db.Businesses.FindAsync([businessId], ct)
            ?? throw new KeyNotFoundException($"Business {businessId} not found");

        var secret = GenerateSecret();
        business.NfcSecret = secret;
        await db.SaveChangesAsync(ct);
        return secret;
    }

    private static string GenerateSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static bool VerifyHmac(string body, string signature, string secret)
    {
        // Expected format: "sha256=<hex>"
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var providedHex = signature["sha256=".Length..];
        var keyBytes = Convert.FromBase64String(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var expectedBytes = HMACSHA256.HashData(keyBytes, bodyBytes);
        var expectedHex = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHex),
            Encoding.ASCII.GetBytes(providedHex.ToLowerInvariant()));
    }

    private record PosPayloadInternal(string? VerificationCode, string? TransactionId);
}
