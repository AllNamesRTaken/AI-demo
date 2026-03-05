using AiDemo.Contracts.DTOs;

namespace AiDemo.Contracts.Hubs;

/// <summary>
/// Client to Server RPC interface - All mutating methods include idempotency key
/// </summary>
public interface IAppHub
{
    // Item Operations
    Task<ItemDto> CreateItemAsync(
        CreateItemDto dto, 
        Guid? idempotencyKey = null);
    
    Task<ItemDto> UpdateItemAsync(
        UpdateItemDto dto, 
        Guid? idempotencyKey = null);
    
    Task DeleteItemAsync(
        Guid id, 
        Guid? idempotencyKey = null);
    
    Task<ItemDto?> GetItemByIdAsync(Guid id);
    
    Task<IEnumerable<ItemDto>> GetItemsAsync();
    
    // Presence
    Task NotifyPresenceAsync(bool isOnline);
}
