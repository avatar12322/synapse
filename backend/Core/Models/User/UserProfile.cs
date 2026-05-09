using Pgvector;

namespace Synapse.Core.Models.User;

public class UserProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // 768-dim embedding vector (sentence-transformers / agents service)
    public Vector? Embedding { get; set; }

    // JSON: ["coffee", "board_games", "tech", ...]
    public string? InterestTags { get; set; }

    // JSON: { "energy": 7, "mood": "curious", "timestamp": "2026-05-09T..." }
    public string? MoodSnapshot { get; set; }

    // Radius the user is willing to travel for a mission (metres)
    public int SearchRadiusMetres { get; set; } = 2000;

    public DateTime LastProfiledAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
