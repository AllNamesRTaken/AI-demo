using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace AvaloniaApp.Services;

public sealed class HubConnectionService : IHubConnectionService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly ILogger<HubConnectionService> _logger;

    public HubConnectionService(ILogger<HubConnectionService> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event EventHandler<ItemDto>? ItemCreated;
    public event EventHandler<ItemDto>? ItemUpdated;
    public event EventHandler<Guid>? ItemDeleted;
    public event EventHandler<NotificationDto>? NotificationReceived;
    public event EventHandler<ErrorDto>? ErrorReceived;
    public event EventHandler<UserPresenceDto>? UserPresenceChanged;
    public event EventHandler<RateLimitInfo>? RateLimitExceeded;
    public event EventHandler<string>? ForceDisconnected;
    public event EventHandler<string>? ConnectionStateChanged;

    public async Task StartAsync(string serverUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            await StopAsync(cancellationToken);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/app", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect(new SignalRRetryPolicy())
            .Build();

        RegisterCallbacks();
        RegisterConnectionEvents();

        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("Connected to SignalR hub");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(cancellationToken);
            await _connection.DisposeAsync();
            _connection = null;
            _logger.LogInformation("Disconnected from SignalR hub");
        }
    }

    public async Task<ItemDto> CreateItemAsync(CreateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<ItemDto>(nameof(IAppHub.CreateItemAsync), dto, idempotencyKey, cancellationToken: ct);
    }

    public async Task<ItemDto> UpdateItemAsync(UpdateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<ItemDto>(nameof(IAppHub.UpdateItemAsync), dto, idempotencyKey, cancellationToken: ct);
    }

    public async Task DeleteItemAsync(Guid id, Guid? idempotencyKey = null, CancellationToken ct = default)
    {
        EnsureConnected();
        await _connection!.InvokeAsync(nameof(IAppHub.DeleteItemAsync), id, idempotencyKey, cancellationToken: ct);
    }

    public async Task<ItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<ItemDto?>(nameof(IAppHub.GetItemByIdAsync), id, cancellationToken: ct);
    }

    public async Task<IEnumerable<ItemDto>> GetItemsAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<IEnumerable<ItemDto>>(nameof(IAppHub.GetItemsAsync), cancellationToken: ct);
    }

    public async Task NotifyPresenceAsync(bool isOnline, CancellationToken ct = default)
    {
        EnsureConnected();
        await _connection!.InvokeAsync(nameof(IAppHub.NotifyPresenceAsync), isOnline, cancellationToken: ct);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to SignalR hub. Call StartAsync first.");
        }
    }

    private void RegisterCallbacks()
    {
        _connection!.On<ItemDto>(nameof(IAppHubClient.OnItemCreated), item =>
        {
            _logger.LogInformation("Item created: {ItemId}", item.Id);
            ItemCreated?.Invoke(this, item);
        });

        _connection.On<ItemDto>(nameof(IAppHubClient.OnItemUpdated), item =>
        {
            _logger.LogInformation("Item updated: {ItemId}", item.Id);
            ItemUpdated?.Invoke(this, item);
        });

        _connection.On<Guid>(nameof(IAppHubClient.OnItemDeleted), itemId =>
        {
            _logger.LogInformation("Item deleted: {ItemId}", itemId);
            ItemDeleted?.Invoke(this, itemId);
        });

        _connection.On<NotificationDto>(nameof(IAppHubClient.OnNotification), notification =>
        {
            _logger.LogInformation("Notification received: {Type}", notification.Type);
            NotificationReceived?.Invoke(this, notification);
        });

        _connection.On<ErrorDto>(nameof(IAppHubClient.OnError), error =>
        {
            _logger.LogError("Error received: {Code} - {Message}", error.Code, error.Message);
            ErrorReceived?.Invoke(this, error);
        });

        _connection.On<UserPresenceDto>(nameof(IAppHubClient.OnUserPresenceChanged), presence =>
        {
            _logger.LogInformation("User presence changed: {UserId} - {IsOnline}", presence.UserId, presence.IsOnline);
            UserPresenceChanged?.Invoke(this, presence);
        });

        _connection.On<RateLimitInfo>(nameof(IAppHubClient.OnRateLimitExceeded), info =>
        {
            _logger.LogWarning("Rate limit exceeded. Remaining: {Remaining}", info.Remaining);
            RateLimitExceeded?.Invoke(this, info);
        });

        _connection.On<string>(nameof(IAppHubClient.OnForceDisconnect), reason =>
        {
            _logger.LogWarning("Server requested disconnect: {Reason}", reason);
            ForceDisconnected?.Invoke(this, reason);
        });
    }

    private void RegisterConnectionEvents()
    {
        _connection!.Closed += async (error) =>
        {
            _logger.LogWarning(error, "Connection closed");
            ConnectionStateChanged?.Invoke(this, "Disconnected");
            await Task.CompletedTask;
        };

        _connection.Reconnecting += async (error) =>
        {
            _logger.LogInformation("Reconnecting...");
            ConnectionStateChanged?.Invoke(this, "Reconnecting");
            await Task.CompletedTask;
        };

        _connection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("Reconnected with ID: {ConnectionId}", connectionId);
            ConnectionStateChanged?.Invoke(this, "Connected");
            await Task.CompletedTask;
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    /// <summary>
    /// Custom retry policy with exponential backoff for SignalR reconnection.
    /// </summary>
    private sealed class SignalRRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Delays =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ];

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return retryContext.PreviousRetryCount < Delays.Length
                ? Delays[retryContext.PreviousRetryCount]
                : TimeSpan.FromMinutes(1);
        }
    }
}
