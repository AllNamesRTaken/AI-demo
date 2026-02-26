namespace AiDemo.Contracts.DTOs.Game;

public sealed record PlayerStateDto(
    Guid UserId,
    string DisplayName,
    float Y,
    float VelocityY,
    bool IsAlive,
    int Score
);
