using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.Hubs;
using AiDemo.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace AiDemo.Server.Middleware;

/// <summary>
/// Rate limiting configuration for SignalR hub connections.
/// ADR-008: Two policies protect against abuse —
///   signalr-user:       Sliding window 100 req/min per authenticated user
///   signalr-connection: Token bucket  20 tokens, replenish 10/s per TCP connection
/// </summary>
public static class SignalRRateLimitingConfiguration
{
    public const string UserPolicy = "signalr-user";
    public const string ConnectionPolicy = "signalr-connection";

    public static IServiceCollection AddSignalRRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-user: SlidingWindow — 100 requests per minute, partitioned by user ID
            options.AddPolicy(UserPolicy, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? httpContext.User.FindFirstValue("sub")
                                  ?? "anonymous",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 12,   // 5-second granularity
                        PermitLimit = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Per-connection: TokenBucket — 20 tokens, replenish 10/sec
            options.AddPolicy(ConnectionPolicy, httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: httpContext.Connection.Id,
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 20,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        TokensPerPeriod = 10,
                        AutoReplenishment = true,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // On rejection: return 429 and attempt a SignalR push to the rejected connection
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Best-effort: notify the client over SignalR (only works for established connections)
                var hubContext = context.HttpContext.RequestServices
                    .GetService<IHubContext<AppHub, IAppHubClient>>();

                if (hubContext is not null)
                {
                    var info = new RateLimitInfo(
                        Limit: 100,
                        Remaining: 0,
                        ResetTime: DateTime.UtcNow.AddSeconds(60));

                    var connectionId = context.HttpContext.Connection.Id;
                    try
                    {
                        await hubContext.Clients.Client(connectionId).OnRateLimitExceeded(info);
                    }
                    catch
                    {
                        // Ignore — connection may not be a SignalR client
                    }
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Rate limit exceeded.", retryAfterSeconds = 60 }, ct);
            };
        });

        return services;
    }
}
