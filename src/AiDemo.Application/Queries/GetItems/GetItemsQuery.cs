using AiDemo.Contracts.DTOs;
using Mediator;

namespace AiDemo.Application.Queries.GetItems;

public sealed record GetItemsQuery(
    Guid UserId
) : IQuery<IEnumerable<ItemDto>>;
