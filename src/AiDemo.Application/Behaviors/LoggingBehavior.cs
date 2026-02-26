using Mediator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AiDemo.Application.Behaviors;

public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<LoggingBehavior<TMessage, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Handling {MessageName}", messageName);

        try
        {
            var response = await next(message, cancellationToken);
            
            sw.Stop();
            _logger.LogInformation(
                "Handled {MessageName} in {ElapsedMs}ms", 
                messageName, 
                sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Error handling {MessageName} after {ElapsedMs}ms",
                messageName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
