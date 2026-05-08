using System.ComponentModel.DataAnnotations;
using UserEntity = Synapse.Core.Models.User.User;

namespace Synapse.Core.Models.Notification;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? RelatedMissionId { get; set; }
}

public enum NotificationType
{
    MissionMatched = 0,
    MissionCompleted = 1,
    MissionExpired = 2,
    FriendRequest = 3,
    Info = 4
}
