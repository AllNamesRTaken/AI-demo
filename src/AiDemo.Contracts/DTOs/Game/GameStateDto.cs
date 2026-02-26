using AiDemo.Contracts.Enums;

namespace AiDemo.Contracts.DTOs.Game;

public sealed record GameStateDto(
    long TickNumber,
    IReadOnlyList<PlayerStateDto> Players,
    IReadOnlyList<PipeDto> Pipes,
    GameStatus Status,
    int CountdownSeconds
);
