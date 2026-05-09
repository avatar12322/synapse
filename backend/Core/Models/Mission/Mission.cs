using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.Models.Mission;

public class Mission
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public MissionCategory Category { get; set; }

    // Where the mission takes place
    public int BusinessId { get; set; }
    public Business.Business Business { get; set; } = null!;

    // Matched participants
    public int? UserAId { get; set; }
    public int? UserBId { get; set; }
    public User.User? UserA { get; set; }
    public User.User? UserB { get; set; }

    // Acceptance tracking — mission moves to Accepted when both are true
    public bool UserAAccepted { get; set; } = false;
    public bool UserBAccepted { get; set; } = false;

    // Discount/reward from the business
    [MaxLength(200)]
    public string? DiscountDescription { get; set; }
    public int DiscountPercent { get; set; } = 15;

    // 6-digit code the business staff enters to verify physical presence
    [MaxLength(6)]
    public string? VerificationCode { get; set; }
    public DateTime? VerificationCodeExpiresAt { get; set; }

    // Phone-lock completion tracking
    public DateTime? LockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RequiredLockMinutes { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    // Stripe payment fields
    [MaxLength(100)]
    public string? StripePaymentIntentId { get; set; }
    public int CommissionAmountCents { get; set; } = 0;
    public bool CommissionPaid { get; set; } = false;

    // Phase 2: POS webhook verification
    public bool VerifiedByPos { get; set; } = false;
    [MaxLength(200)]
    public string? PosTransactionId { get; set; }

    // AI generation metadata
    public string? AiPromptUsed { get; set; }
    public string? InterestTags { get; set; }  // JSON array of shared interests

    // H3 geo index (phase 3 sharding prep — set at creation from business location)
    [MaxLength(20)]
    public string? H3Index { get; set; }
}

public enum MissionStatus
{
    Pending = 0,      // Generated, waiting for user acceptance
    Accepted = 1,     // Both users accepted
    InProgress = 2,   // Users at venue, phones locked
    Completed = 3,    // Session finished, reward granted
    Expired = 4,
    Cancelled = 5
}

public enum MissionCategory
{
    Coffee = 0,
    Lunch = 1,
    Sports = 2,
    Culture = 3,
    Learning = 4,
    Networking = 5,
    Other = 6
}
