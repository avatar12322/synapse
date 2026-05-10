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

    // Phase 3 GDPR: user's last known location as H3 cell, degraded res 9→7 after 30 days (RODO art. 5)
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string? LastKnownH3 { get; set; }
    public DateTime? LastKnownH3CapturedAt { get; set; }

    // Phase 4 TEE-ready: AES-256-GCM encrypted copy of Embedding (layout: 12B nonce | ciphertext | 16B tag)
    // Key lives exclusively in .NET backend — never in Python or DB.
    public byte[]? EncryptedEmbedding { get; set; }

    public DateTime LastProfiledAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
