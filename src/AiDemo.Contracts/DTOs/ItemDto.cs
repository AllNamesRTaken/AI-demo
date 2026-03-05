namespace AiDemo.Contracts.DTOs;

public sealed record ItemDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedByUserId
);
