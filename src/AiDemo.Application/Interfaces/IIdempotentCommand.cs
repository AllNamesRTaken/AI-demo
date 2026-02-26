using Mediator;

namespace AiDemo.Application.Interfaces;

public interface IIdempotentCommand<out TResponse> : ICommand<TResponse>
{
    Guid? IdempotencyKey { get; }
}
