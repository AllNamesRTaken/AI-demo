namespace AiDemo.Contracts.DTOs;

public sealed record ErrorDto(
    string Code,
    string Message,
    string? Details = null,
    DateTime Timestamp = default
);
