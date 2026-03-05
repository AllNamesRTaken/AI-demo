namespace AiDemo.Contracts.DTOs;

public sealed record UpdateItemDto(
    Guid Id,
    string Name,
    string Description
);
