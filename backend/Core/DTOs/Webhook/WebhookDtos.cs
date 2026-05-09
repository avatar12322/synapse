using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.DTOs.Webhook;

public record PosWebhookPayload(
    [Required] string Event,
    [Required] string BusinessId,
    [Required, MinLength(6), MaxLength(6)] string VerificationCode,
    int Amount,
    string Currency,
    string TransactionId,
    string Timestamp
);

public record PosWebhookResponse(bool Success, string? MissionId, string? Error);

public record GenerateSecretResponse(string Secret, string SecretType);
