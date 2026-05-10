namespace Synapse.Core.Services.Presence;

public interface IPresencePublisher
{
    Task PublishAsync(int missionId, string json, CancellationToken ct = default);
    Task DeleteStreamAsync(int missionId, CancellationToken ct = default);
}
