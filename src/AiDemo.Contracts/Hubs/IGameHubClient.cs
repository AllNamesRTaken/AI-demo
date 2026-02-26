using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.DTOs.Game;

namespace AiDemo.Contracts.Hubs;

/// <summary>
/// Defines the server-to-client SignalR callback contract for the Flappy Bird game.
/// Implemented by connected clients.
/// </summary>
public interface IGameHubClient
{
    // --- Game callbacks ---

    /// <summary>Called when room membership or status changes.</summary>
    Task OnRoomUpdated(GameRoomDto room);

    /// <summary>Called when the game transitions from Countdown to Running.</summary>
    Task OnGameStarted(GameStateDto state);

    /// <summary>Called once per second during the 3-second countdown.</summary>
    Task OnCountdown(int secondsRemaining);

    /// <summary>Called every 50 ms with the latest game state (all birds + pipes).</summary>
    Task OnGameTick(GameStateDto state);

    /// <summary>Called when all birds are dead and the game has ended.</summary>
    Task OnGameEnded(GameResultDto result);

    /// <summary>Called when a new player joins the room.</summary>
    Task OnPlayerJoined(PlayerStateDto player);

    /// <summary>Called when a player leaves the room.</summary>
    Task OnPlayerLeft(Guid userId);

    // --- System callbacks ---

    /// <summary>Called when the server sends an error to this specific client.</summary>
    Task OnError(ErrorDto error);

    /// <summary>Called for general server notifications (info/warning/system messages).</summary>
    Task OnNotification(NotificationDto notification);

    /// <summary>Called when this client has exceeded the rate limit.</summary>
    Task OnRateLimitExceeded(RateLimitInfo info);

    /// <summary>Called when the server forcibly disconnects this client.</summary>
    Task OnForceDisconnect(string reason);
}
