namespace Synapse.Core.Models.User;

public enum ReputationReason
{
    MissionCompleted = 0,
    NfcVerification = 1,
    FirstMission = 2,
    Streak = 3,
}

public class ReputationTransaction
{
    public int Id { get; set; }

    public int UserReputationId { get; set; }
    public UserReputation UserReputation { get; set; } = null!;

    public int Points { get; set; }
    public ReputationReason Reason { get; set; }
    public int? MissionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
