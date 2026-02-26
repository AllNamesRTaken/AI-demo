using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.Hubs;
using AiDemo.Application.Commands.CreateItem;
using AiDemo.Application.Commands.UpdateItem;
using AiDemo.Application.Commands.DeleteItem;
using AiDemo.Application.Queries.GetItems;
using AiDemo.Application.Queries.GetItemById;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AiDemo.Server.Hubs;

[Authorize]
[EnableRateLimiting("signalr-user")]
public sealed class AppHub : Hub<IAppHubClient>, IAppHub
{
    private readonly IMediator _mediator;
    private readonly ILogger<AppHub> _logger;

    public AppHub(IMediator mediator, ILogger<AppHub> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ItemDto> CreateItemAsync(
        CreateItemDto dto,
        Guid? idempotencyKey = null)
    {
        var userId = GetUserId();
        
        var command = new CreateItemCommand(
            dto.Name,
            dto.Description,
            userId,
            idempotencyKey);

        var result = await _mediator.Send(command);

        // Notifications are sent via the Outbox Pattern (ADR-005)
        // OutboxProcessorService picks up outbox messages and dispatches SignalR notifications

        return result;
    }

    public async Task<ItemDto> UpdateItemAsync(
        UpdateItemDto dto,
        Guid? idempotencyKey = null)
    {
        var userId = GetUserId();
        
        var command = new UpdateItemCommand(
            dto.Id,
            dto.Name,
            dto.Description,
            userId,
            idempotencyKey);

        var result = await _mediator.Send(command);

        // Notifications are sent via the Outbox Pattern (ADR-005)

        return result;
    }

    public async Task DeleteItemAsync(
        Guid id,
        Guid? idempotencyKey = null)
    {
        var userId = GetUserId();
        
        var command = new DeleteItemCommand(id, userId, idempotencyKey);

        await _mediator.Send(command);

        // Notifications are sent via the Outbox Pattern (ADR-005)
    }

    public async Task<ItemDto?> GetItemByIdAsync(Guid id)
    {
        var userId = GetUserId();
        
        var query = new GetItemByIdQuery(id, userId);
        return await _mediator.Send(query);
    }

    [EnableRateLimiting("signalr-connection")]
    public async Task<IEnumerable<ItemDto>> GetItemsAsync()
    {
        var userId = GetUserId();
        _logger.LogDebug("GetItemsAsync called for user {UserId}", userId);

        var query = new GetItemsQuery(userId);
        var result = await _mediator.Send(query);

        _logger.LogDebug("GetItemsAsync returning {Count} items", result.Count());
        return result;
    }

    public async Task NotifyPresenceAsync(bool isOnline)
    {
        var userId = GetUserId();
        var userName = Context.User?.Identity?.Name ?? "Unknown";

        var presence = new UserPresenceDto(
            userId,
            userName,
            isOnline,
            DateTime.UtcNow);

        await Clients.Others.OnUserPresenceChanged(presence);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            exception,
            "Client disconnected: {ConnectionId}",
            Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId()
    {
        // Try standard NameIdentifier claim first
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Fall back to "sub" claim (common in OIDC)
        if (string.IsNullOrEmpty(userIdClaim))
        {
            userIdClaim = Context.User?.FindFirst("sub")?.Value;
        }
        
        _logger.LogDebug("User claims: {Claims}", 
            string.Join(", ", Context.User?.Claims.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>()));
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogError("No user ID claim found. Available claims: {Claims}",
                string.Join(", ", Context.User?.Claims.Select(c => c.Type) ?? Array.Empty<string>()));
            throw new UnauthorizedAccessException("User ID not found in claims");
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogError("User ID claim is not a valid GUID: {UserIdClaim}", userIdClaim);
            throw new UnauthorizedAccessException($"User ID '{userIdClaim}' is not a valid GUID");
        }

        return userId;
    }
}
