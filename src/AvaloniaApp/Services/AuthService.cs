using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.OidcClient;
using IdentityModel.Client;

namespace AvaloniaApp.Services;

public sealed class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly OidcClient _oidcClient;
    private string? _accessToken;
    private string? _refreshToken;
    private string? _username;
    private Guid? _userId;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
        Debug.WriteLine("=== AuthService constructor starting ===");

        try
        {
            var options = new OidcClientOptions
            {
                Authority = "http://webinfo.local:9000/application/o/ai-demo/",
                ClientId = "ai-demo-desktop",
                Scope = "openid profile email",
                RedirectUri = "http://localhost:7890/callback",
                Browser = new SystemBrowser(7890),
                Policy = new Policy
                {
                    Discovery = new DiscoveryPolicy
                    {
                        RequireHttps = false, // Development only
                        ValidateEndpoints = false // Authentik uses shared endpoints
                    }
                }
            };

            Debug.WriteLine($"Creating OidcClient with Authority: {options.Authority}");
            _oidcClient = new OidcClient(options);
            Debug.WriteLine("OidcClient created successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR in AuthService constructor: {ex}");
            throw;
        }
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public string? Username => _username;
    public Guid? UserId => _userId;

    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine("=== LoginAsync called ===");
            _logger.LogInformation("Starting OIDC login flow");

            Debug.WriteLine("Calling _oidcClient.LoginAsync...");
            var result = await _oidcClient.LoginAsync(new LoginRequest(), cancellationToken);
            Debug.WriteLine($"LoginAsync returned, IsError: {result.IsError}");

            if (result.IsError)
            {
                _logger.LogError("OIDC login failed - Error: {Error}, ErrorDescription: {ErrorDescription}", 
                    result.Error, 
                    result.ErrorDescription);
                Debug.WriteLine($"ERROR: {result.Error}, Description: {result.ErrorDescription}");
                return false;
            }

            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            _username = result.User.Identity?.Name ?? "Unknown";
            
            // Extract user ID from claims
            var subClaim = result.User.FindFirst("sub");
            if (subClaim != null && Guid.TryParse(subClaim.Value, out var userId))
            {
                _userId = userId;
            }
            else
            {
                _userId = Guid.NewGuid(); // Fallback
            }

            _logger.LogInformation("OIDC login successful for user: {Username}, UserId: {UserId}", _username, _userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC login failed with exception");
            return false;
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _accessToken = null;
        _refreshToken = null;
        _username = null;
        _userId = null;

        _logger.LogInformation("User logged out");
        return Task.CompletedTask;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            return false;
        }

        try
        {
            var result = await _oidcClient.RefreshTokenAsync(_refreshToken, cancellationToken: cancellationToken);

            if (result.IsError)
            {
                _logger.LogError("Token refresh failed: {Error}", result.Error);
                return false;
            }

            _accessToken = result.AccessToken;
            _refreshToken = result.RefreshToken;
            
            _logger.LogInformation("Token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return false;
        }
    }
}
