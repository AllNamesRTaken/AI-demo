using AiDemo.Contracts.Enums;

namespace AiDemo.Contracts.DTOs.Game;

public sealed record GameRoomDto(
    string RoomId,
    string Passphrase,
    IReadOnlyList<PlayerStateDto> Players,
    GameStatus Status,
    int MaxPlayers
);
