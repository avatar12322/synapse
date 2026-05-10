namespace Synapse.Core.Models.User;

public class UserReputation
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int TotalPoints { get; set; } = 0;

    // 1–5 derived from TotalPoints: 1=0-99, 2=100-299, 3=300-699, 4=700-1499, 5=1500+
    public int RepLevel { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReputationTransaction> Transactions { get; set; } = new List<ReputationTransaction>();

    public static int CalculateLevel(int points) => points switch
    {
        >= 1500 => 5,
        >= 700  => 4,
        >= 300  => 3,
        >= 100  => 2,
        _       => 1,
    };

    // Points needed to reach the NEXT level (0 if already max)
    public static int NextLevelThreshold(int level) => level switch
    {
        1 => 100,
        2 => 300,
        3 => 700,
        4 => 1500,
        _ => 0,
    };
}
