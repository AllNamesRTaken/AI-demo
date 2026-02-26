using AiDemo.Contracts.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApp.Services;

public interface IHubConnectionService
{
    Task StartAsync(string serverUrl, string accessToken, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsConnected { get; }
    
    // Item Operations
    Task<ItemDto> CreateItemAsync(CreateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default);
    Task<ItemDto> UpdateItemAsync(UpdateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default);
    Task DeleteItemAsync(Guid id, Guid? idempotencyKey = null, CancellationToken ct = default);
    Task<ItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ItemDto>> GetItemsAsync(CancellationToken ct = default);
    
    // Presence
    Task NotifyPresenceAsync(bool isOnline, CancellationToken ct = default);
    
    // Server-to-Client Events
    event EventHandler<ItemDto>? ItemCreated;
    event EventHandler<ItemDto>? ItemUpdated;
    event EventHandler<Guid>? ItemDeleted;
    event EventHandler<NotificationDto>? NotificationReceived;
    event EventHandler<ErrorDto>? ErrorReceived;
    event EventHandler<UserPresenceDto>? UserPresenceChanged;
    event EventHandler<RateLimitInfo>? RateLimitExceeded;
    
    // Connection lifecycle events
    event EventHandler<string>? ForceDisconnected;
    event EventHandler<string>? ConnectionStateChanged;
}
