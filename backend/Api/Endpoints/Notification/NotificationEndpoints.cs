using System.Security.Claims;
using Synapse.Core.Services.Notification;

namespace Synapse.Api.Endpoints.Notification;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, INotificationService svc) =>
        {
            var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifications = await svc.GetUnreadAsync(userId);
            return Results.Ok(notifications);
        });

        group.MapPost("/read-all", async (ClaimsPrincipal user, INotificationService svc) =>
        {
            var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await svc.MarkAllReadAsync(userId);
            return Results.Ok();
        });
    }
}
