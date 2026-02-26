using AiDemo.Contracts.Responses;

namespace AiDemo.Server.Services;

/// <summary>
/// Interface for OIDC token operations (token refresh via the OIDC provider).
/// </summary>
public interface IOidcTokenService
{
    Task<OidcTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an OIDC token operation.
/// </summary>
public sealed record OidcTokenResult(
    bool IsSuccess,
    TokenResponse? Value = null,
    string? Error = null
);
