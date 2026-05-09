using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.DTOs.Mission;

public record MissionDto(
    int Id,
    string Title,
    string Description,
    string Status,
    string Category,
    int BusinessId,
    string BusinessName,
    string BusinessAddress,
    double BusinessLatitude,
    double BusinessLongitude,
    int? UserAId,
    int? UserBId,
    bool UserAAccepted,
    bool UserBAccepted,
    string? DiscountDescription,
    int DiscountPercent,
    string? VerificationCode,
    DateTime? LockedAt,
    DateTime? CompletedAt,
    int RequiredLockMinutes,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? InterestTags
);

public record AcceptMissionRequest(int MissionId);

public record LockPhonesRequest(int MissionId);

// Business staff calls this to verify physical presence
public record VerifyMissionRequest(
    [Required, MinLength(6), MaxLength(6)] string Code
);

public record MissionSummaryDto(
    int Id,
    string Title,
    string Status,
    string Category,
    string BusinessName,
    int DiscountPercent,
    DateTime ExpiresAt,
    bool IsMyTurn  // true if this user hasn't accepted yet
);
