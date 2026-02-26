namespace AiDemo.Application.Interfaces;

/// <summary>
/// Dispatches outbox messages as SignalR notifications.
/// Implemented in the Server layer where IHubContext is available.
/// </summary>
public interface IOutboxNotificationDispatcher
{
    Task DispatchAsync(string type, string payload, CancellationToken cancellationToken = default);
}
