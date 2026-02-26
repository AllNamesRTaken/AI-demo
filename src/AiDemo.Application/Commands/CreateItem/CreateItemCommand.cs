using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using Mediator;

namespace AiDemo.Application.Commands.CreateItem;

public sealed record CreateItemCommand(
    string Name,
    string Description,
    Guid UserId,
    Guid? IdempotencyKey = null
) : IIdempotentCommand<ItemDto>;
