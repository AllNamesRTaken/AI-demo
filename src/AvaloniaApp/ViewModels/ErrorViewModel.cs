using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

/// <summary>
/// Represents a dismissible error notification, suitable for toast/snackbar display.
/// Wire to IHubConnectionService.ErrorReceived to surface hub errors in the UI.
/// </summary>
public sealed partial class ErrorViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string? _errorCode;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _traceId;

    [ObservableProperty]
    private bool _isWarning;

    /// <summary>Show an error from the hub ErrorReceived event.</summary>
    public void ShowError(string message, string? code = null, string? traceId = null, bool isWarning = false)
    {
        ErrorCode = code;
        ErrorMessage = message;
        TraceId = traceId;
        IsWarning = isWarning;
        IsVisible = true;
    }

    /// <summary>Show a rate-limit warning.</summary>
    public void ShowRateLimitWarning(int retryAfterSeconds)
    {
        ErrorCode = "RATE_LIMITED";
        ErrorMessage = $"Too many requests. Please wait {retryAfterSeconds} seconds before retrying.";
        TraceId = null;
        IsWarning = true;
        IsVisible = true;
    }

    [RelayCommand]
    private void Dismiss()
    {
        IsVisible = false;
        ErrorCode = null;
        ErrorMessage = null;
        TraceId = null;
    }
}
