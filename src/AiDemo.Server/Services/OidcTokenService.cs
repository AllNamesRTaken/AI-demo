using AiDemo.Contracts.Responses;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiDemo.Server.Services;

/// <summary>
/// OIDC token service that communicates with the OIDC provider (Authentik/Cloud)
/// to refresh access tokens.
/// </summary>
public sealed class OidcTokenService : IOidcTokenService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OidcTokenService> _logger;
    private string? _tokenEndpoint;

    public OidcTokenService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OidcTokenService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OidcTokenResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenEndpoint = await GetTokenEndpointAsync(cancellationToken);
            if (tokenEndpoint is null)
            {
                return new OidcTokenResult(false, Error: "Failed to discover OIDC token endpoint");
            }

            var clientId = _configuration["Oidc:ClientId"]
                ?? throw new InvalidOperationException("Oidc:ClientId not configured");

            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestBody, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Token refresh failed with status {StatusCode}: {Response}",
                    response.StatusCode,
                    responseJson);
                return new OidcTokenResult(false, Error: $"Token refresh failed: {response.StatusCode}");
            }

            var tokenResponse = JsonSerializer.Deserialize<OidcTokenResponse>(responseJson);
            if (tokenResponse is null)
            {
                return new OidcTokenResult(false, Error: "Failed to parse token response");
            }

            return new OidcTokenResult(true, Value: new TokenResponse(
                AccessToken: tokenResponse.AccessToken,
                RefreshToken: tokenResponse.RefreshToken ?? refreshToken,
                ExpiresIn: tokenResponse.ExpiresIn,
                TokenType: tokenResponse.TokenType ?? "Bearer"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return new OidcTokenResult(false, Error: ex.Message);
        }
    }

    private async Task<string?> GetTokenEndpointAsync(CancellationToken cancellationToken)
    {
        if (_tokenEndpoint is not null)
            return _tokenEndpoint;

        var authority = _configuration["Oidc:Authority"]
            ?? throw new InvalidOperationException("Oidc:Authority not configured");

        var discoveryUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";

        try
        {
            var response = await _httpClient.GetStringAsync(discoveryUrl, cancellationToken);
            var doc = JsonSerializer.Deserialize<JsonElement>(response);

            if (doc.TryGetProperty("token_endpoint", out var endpoint))
            {
                _tokenEndpoint = endpoint.GetString();
                _logger.LogInformation("Discovered OIDC token endpoint: {TokenEndpoint}", _tokenEndpoint);
                return _tokenEndpoint;
            }

            _logger.LogError("OIDC discovery document missing token_endpoint");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch OIDC discovery document from {Url}", discoveryUrl);
            return null;
        }
    }

    /// <summary>
    /// Internal DTO for deserializing the OIDC provider's token response.
    /// </summary>
    private sealed record OidcTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = default!;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
