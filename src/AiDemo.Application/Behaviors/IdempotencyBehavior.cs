using AiDemo.Application.Interfaces;
using Mediator;

namespace AiDemo.Application.Behaviors;

public sealed class IdempotencyBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IIdempotencyService _idempotencyService;

    public IdempotencyBehavior(IIdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only handle commands with idempotency keys
        if (message is not IIdempotentCommand<TResponse> idempotentCommand 
            || idempotentCommand.IdempotencyKey == null)
        {
            return await next(message, cancellationToken);
        }

        var key = idempotentCommand.IdempotencyKey.Value;

        // Check if already processed (only for reference types)
        if (!typeof(TResponse).IsValueType)
        {
            var cachedResult = await _idempotencyService.GetCachedResultAsync<object>(key, cancellationToken);
            if (cachedResult != null && cachedResult is TResponse typed)
            {
                return typed;
            }
        }

        // Process and cache result
        var result = await next(message, cancellationToken);
        
        // Store result (only for reference types)
        if (!typeof(TResponse).IsValueType && result != null)
        {
            await _idempotencyService.StoreResultAsync(key, (object)result, cancellationToken);
        }

        return result;
    }
}
