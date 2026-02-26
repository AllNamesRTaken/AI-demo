namespace AiDemo.Contracts.DTOs.Game;

public sealed record JoinRoomDto(
    string Passphrase,
    string DisplayName
);
