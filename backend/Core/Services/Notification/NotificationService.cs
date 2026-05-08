using Microsoft.EntityFrameworkCore;
using Synapse.Infrastructure.Data;
using NotificationModel = Synapse.Core.Models.Notification.Notification;
using NotificationTypeEnum = Synapse.Core.Models.Notification.NotificationType;

namespace Synapse.Core.Services.Notification;

public interface INotificationService
{
    Task CreateAsync(int userId, string title, string message, NotificationTypeEnum type, int? relatedMissionId = null);
    Task<List<NotificationModel>> GetUnreadAsync(int userId);
    Task MarkAllReadAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly SynapseDbContext _db;

    public NotificationService(SynapseDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(int userId, string title, string message, NotificationTypeEnum type, int? relatedMissionId = null)
    {
        _db.Notifications.Add(new NotificationModel
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedMissionId = relatedMissionId
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<NotificationModel>> GetUnreadAsync(int userId) =>
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

    public async Task MarkAllReadAsync(int userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
