using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using Mediator;

namespace AiDemo.Application.Commands.UpdateItem;

public sealed record UpdateItemCommand(
    Guid Id,
    string Name,
    string Description,
    Guid UserId,
    Guid? IdempotencyKey = null
) : IIdempotentCommand<ItemDto>;
