namespace AiDemo.Contracts.DTOs;

public sealed record RateLimitInfo(
    int Limit,
    int Remaining,
    DateTime ResetTime
);
