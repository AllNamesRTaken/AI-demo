using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.Hubs;
using AiDemo.Application.Interfaces;
using AiDemo.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace AiDemo.Server.Services;

/// <summary>
/// Dispatches outbox messages as SignalR notifications to connected clients.
/// </summary>
public sealed class OutboxNotificationDispatcher : IOutboxNotificationDispatcher
{
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext;
    private readonly ILogger<OutboxNotificationDispatcher> _logger;

    public OutboxNotificationDispatcher(
        IHubContext<AppHub, IAppHubClient> hubContext,
        ILogger<OutboxNotificationDispatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task DispatchAsync(string type, string payload, CancellationToken cancellationToken = default)
    {
        switch (type)
        {
            case "ItemCreated":
                var createdItem = JsonSerializer.Deserialize<ItemDto>(payload);
                if (createdItem != null)
                {
                    await _hubContext.Clients.All.OnItemCreated(createdItem);
                    _logger.LogInformation("Dispatched ItemCreated notification for item {ItemId}", createdItem.Id);
                }
                break;

            case "ItemUpdated":
                var updatedItem = JsonSerializer.Deserialize<ItemDto>(payload);
                if (updatedItem != null)
                {
                    await _hubContext.Clients.All.OnItemUpdated(updatedItem);
                    _logger.LogInformation("Dispatched ItemUpdated notification for item {ItemId}", updatedItem.Id);
                }
                break;

            case "ItemDeleted":
                var deletedId = JsonSerializer.Deserialize<Guid>(payload);
                await _hubContext.Clients.All.OnItemDeleted(deletedId);
                _logger.LogInformation("Dispatched ItemDeleted notification for item {ItemId}", deletedId);
                break;

            default:
                _logger.LogWarning("Unknown outbox message type: {Type}", type);
                break;
        }
    }
}
