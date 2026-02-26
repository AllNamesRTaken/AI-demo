using AiDemo.Application.Interfaces;
using Mediator;

namespace AiDemo.Application.Commands.DeleteItem;

public sealed record DeleteItemCommand(
    Guid Id,
    Guid UserId,
    Guid? IdempotencyKey = null
) : IIdempotentCommand<Unit>;
