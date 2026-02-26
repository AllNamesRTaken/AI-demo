namespace AiDemo.Contracts.DTOs;

public sealed record NotificationDto(
    string Type,
    string Message,
    DateTime Timestamp,
    object? Data = null
);
