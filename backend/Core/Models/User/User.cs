using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.Models.User;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    [MaxLength(10)]
    public string Language { get; set; } = "en";

    [MaxLength(100)]
    public string? StripeCustomerId { get; set; }

    // Phase 3 Anti-Sybil: device signals captured at login / match request
    [MaxLength(200)]
    public string? DeviceFingerprint { get; set; }   // SHA-256(deviceId+platform+model) from X-Device-Fingerprint header

    [MaxLength(45)]
    public string? LastKnownIp { get; set; }          // IPv4 or IPv6

    [MaxLength(17)]
    public string? LastKnownBssid { get; set; }       // WiFi BSSID from presence-verifier plugin (optional)

    public ICollection<Social.Friendship> InitiatedFriendships { get; set; } = new List<Social.Friendship>();
    public ICollection<Social.Friendship> ReceivedFriendships { get; set; } = new List<Social.Friendship>();
}
