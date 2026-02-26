namespace AiDemo.Domain.Events;

public sealed record ItemCreatedEvent(
    Guid ItemId,
    string Name,
    string Description,
    Guid CreatedByUserId,
    DateTime CreatedAt
);
