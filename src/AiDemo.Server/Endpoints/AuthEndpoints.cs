using AiDemo.Contracts.DTOs;
using AiDemo.Contracts.Requests;
using AiDemo.Server.Services;

namespace AiDemo.Server.Endpoints;

/// <summary>
/// Authentication endpoint for token refresh operations.
/// This is one of the few REST endpoints (per ADR-004) alongside health checks.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        auth.MapPost("/refresh", RefreshTokenAsync)
            .WithName("RefreshToken")
            .WithDescription("Exchange a refresh token for a new access token")
            .Produces<AiDemo.Contracts.Responses.TokenResponse>()
            .Produces<ErrorDto>(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        IOidcTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new ErrorDto(
                "INVALID_REQUEST",
                "Refresh token is required"));
        }

        var result = await tokenService.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return Results.BadRequest(new ErrorDto(
            "TOKEN_REFRESH_FAILED",
            result.Error ?? "Failed to refresh token"));
    }
}
