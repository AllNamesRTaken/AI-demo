namespace AiDemo.Contracts.DTOs;

public sealed record UserPresenceDto(
    Guid UserId,
    string Username,
    bool IsOnline,
    DateTime LastSeen
);
