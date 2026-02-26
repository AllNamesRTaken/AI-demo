using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApp.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);
    bool IsAuthenticated { get; }
    string? Username { get; }
    Guid? UserId { get; }
}
