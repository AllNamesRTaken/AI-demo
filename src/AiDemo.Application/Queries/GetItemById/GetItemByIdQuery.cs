using AiDemo.Contracts.DTOs;
using Mediator;

namespace AiDemo.Application.Queries.GetItemById;

public sealed record GetItemByIdQuery(
    Guid Id,
    Guid UserId
) : IQuery<ItemDto?>;
