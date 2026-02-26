using AiDemo.Contracts.DTOs;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AvaloniaApp.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<LoginViewModel> _logger;

    public event EventHandler? LoginSucceeded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoggingIn;

    public LoginViewModel(IAuthService authService, ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoggingIn = true;

        _logger.LogInformation("=== LoginCommand executing ===");

        try
        {
            _logger.LogInformation("Calling AuthService.LoginAsync");
            
            // Note: username and password parameters are not used by OIDC flow
            var success = await _authService.LoginAsync(string.Empty, string.Empty);
            
            _logger.LogInformation("AuthService.LoginAsync returned: {Success}", success);
            
            if (!success)
            {
                ErrorMessage = "Authentication failed. Please try again.";
                _logger.LogWarning("Authentication failed");
            }
            else
            {
                _logger.LogInformation("Authentication succeeded, invoking LoginSucceeded event");
                // Notify that login succeeded
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed with exception in LoginViewModel");
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            _logger.LogInformation("LoginCommand completed, setting IsLoggingIn = false");
            IsLoggingIn = false;
        }
    }
}
