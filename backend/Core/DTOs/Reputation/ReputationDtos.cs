namespace Synapse.Core.DTOs.Reputation;

public record UserReputationDto(
    int UserId,
    int TotalPoints,
    int RepLevel,
    int PointsToNextLevel,
    double ProgressPercent,
    DateTime UpdatedAt
);

public record ReputationTransactionDto(
    int Id,
    int Points,
    string Reason,
    int? MissionId,
    DateTime CreatedAt
);
