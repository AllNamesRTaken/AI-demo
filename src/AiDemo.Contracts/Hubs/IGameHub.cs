using AiDemo.Contracts.DTOs.Game;

namespace AiDemo.Contracts.Hubs;

/// <summary>
/// Defines the client-to-server SignalR hub contract for the Flappy Bird game.
/// Implemented by GameHub on the server side.
/// </summary>
public interface IGameHub
{
    /// <summary>
    /// Creates a new room with the given passphrase or joins an existing one.
    /// Mediator-path: idempotent (safe to call on reconnect with same key).
    /// </summary>
    Task<GameRoomDto> CreateOrJoinRoomAsync(
        JoinRoomDto dto,
        Guid? idempotencyKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Leaves the current room. Mediator-path.
    /// </summary>
    Task LeaveRoomAsync(CancellationToken ct = default);

    /// <summary>
    /// Toggles the current player's ready state. Direct-path (no Mediator overhead).
    /// </summary>
    Task SetReadyAsync(CancellationToken ct = default);

    /// <summary>
    /// Enqueues a flap action for the current player's bird. Direct-path, fire-and-forget.
    /// </summary>
    Task FlapAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the all-time leaderboard. Mediator-path (DB query).
    /// </summary>
    Task<IReadOnlyList<GameResultDto>> GetLeaderboardAsync(CancellationToken ct = default);
}
