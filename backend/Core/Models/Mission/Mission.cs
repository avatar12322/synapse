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

    // Discount/reward from the business
    [MaxLength(200)]
    public string? DiscountDescription { get; set; }
    public int DiscountPercent { get; set; } = 0;

    // Phone-lock completion tracking
    public DateTime? LockedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RequiredLockMinutes { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    // AI generation metadata
    public string? AiPromptUsed { get; set; }
    public string? InterestTags { get; set; } // JSON array of shared interests
}

public enum MissionStatus
{
    Pending = 0,       // Generated, waiting for user acceptance
    Accepted = 1,      // Both users accepted
    InProgress = 2,    // Users at venue, phones locked
    Completed = 3,     // Session finished, reward granted
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
