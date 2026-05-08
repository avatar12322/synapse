using UserEntity = Synapse.Core.Models.User.User;

namespace Synapse.Core.Models.Social;

public class Friendship
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FriendId { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public UserEntity Friend { get; set; } = null!;
}

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
