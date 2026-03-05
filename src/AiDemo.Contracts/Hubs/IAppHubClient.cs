using AiDemo.Contracts.DTOs;

namespace AiDemo.Contracts.Hubs;

/// <summary>
/// Server to Client callback interface
/// </summary>
public interface IAppHubClient
{
    // Item Notifications
    Task OnItemCreated(ItemDto item);
    Task OnItemUpdated(ItemDto item);
    Task OnItemDeleted(Guid itemId);
    
    // System Notifications
    Task OnNotification(NotificationDto notification);
    Task OnError(ErrorDto error);
    
    // Presence
    Task OnUserPresenceChanged(UserPresenceDto presence);
    
    // Rate Limiting
    Task OnRateLimitExceeded(RateLimitInfo info);
    
    // System
    Task OnForceDisconnect(string reason);
}
