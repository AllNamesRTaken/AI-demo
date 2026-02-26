namespace AiDemo.Contracts.DTOs.Game;

public sealed record PlayerResultDto(
    int Rank,
    Guid UserId,
    string DisplayName,
    int Score
);

public sealed record GameResultDto(
    string RoomId,
    IReadOnlyList<PlayerResultDto> Results,
    DateTime EndedAt
);
