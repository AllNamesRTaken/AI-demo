using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApp.Services;

/// <summary>
/// Wraps hub operations with a Polly v8 resilience pipeline:
///   - Timeout:          30 s per attempt
///   - Retry:            up to 4 attempts with exponential backoff (1s, 2s, 4s, 8s) + jitter
///   - Circuit breaker:  opens after 50 % failure rate over 30 s (min 5 calls); recovers after 30 s
/// </summary>
public interface IHubOperationPolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken ct = default);
    Task ExecuteAsync(Func<CancellationToken, ValueTask> operation, CancellationToken ct = default);
}

public sealed class HubOperationPolicy : IHubOperationPolicy
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<HubOperationPolicy> _logger;

    public HubOperationPolicy(ILogger<HubOperationPolicy> logger)
    {
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            // 1. Timeout per attempt (innermost — wraps each individual try)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                OnTimeout = args =>
                {
                    _logger.LogWarning("Hub operation timed out after {Timeout}s", 30);
                    return ValueTask.CompletedTask;
                }
            })
            // 2. Retry with exponential backoff
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Hub operation failed (attempt {Attempt}). Retrying in {Delay}...",
                        args.AttemptNumber + 1,
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            // 3. Circuit breaker (outermost — prevents calls when system is unhealthy)
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError("Circuit breaker OPENED. Hub operations blocked for 30s.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker closed. Hub operations resumed.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker half-open. Testing hub connectivity...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> operation, CancellationToken ct = default)
        => _pipeline.ExecuteAsync(operation, ct).AsTask();

    public Task ExecuteAsync(Func<CancellationToken, ValueTask> operation, CancellationToken ct = default)
        => _pipeline.ExecuteAsync(operation, ct).AsTask();
}
