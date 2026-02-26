using AiDemo.Contracts.DTOs;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AvaloniaApp.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IHubConnectionService _hubConnection;
    private readonly IAuthService _authService;
    private readonly IIdempotencyKeyService _idempotencyKeyService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ErrorViewModel _error;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private ItemViewModel? _selectedItem;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<ItemViewModel> Items { get; } = new();
    
    /// <summary>Bindable error notification view model for toast/snackbar display.</summary>
    public ErrorViewModel Error => _error;

    public MainWindowViewModel(
        IHubConnectionService hubConnection,
        IAuthService authService,
        IIdempotencyKeyService idempotencyKeyService,
        ILogger<MainWindowViewModel> logger,
        ErrorViewModel error)
    {
        _hubConnection = hubConnection;
        _authService = authService;
        _idempotencyKeyService = idempotencyKeyService;
        _logger = logger;
        _error = error;

        // Subscribe to hub events
        _hubConnection.ItemCreated += OnItemCreated;
        _hubConnection.ItemUpdated += OnItemUpdated;
        _hubConnection.ItemDeleted += OnItemDeleted;
        _hubConnection.NotificationReceived += OnNotificationReceived;
        _hubConnection.ErrorReceived += OnErrorReceived;
        _hubConnection.RateLimitExceeded += OnRateLimitExceeded;
        
        // Auto-connect on startup
        _ = InitializeAsync();
    }
    
    private async Task InitializeAsync()
    {
        await Task.Delay(100); // Small delay to let UI render
        await ConnectAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsAuthenticated = _authService.IsAuthenticated;
            Username = _authService.Username;

            if (!IsAuthenticated)
            {
                StatusMessage = "Please login first";
                return;
            }

            var token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage = "Failed to get access token";
                return;
            }

            await _hubConnection.StartAsync("http://localhost:5000", token);
            IsConnected = _hubConnection.IsConnected;
            StatusMessage = "Connected to server";

            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _hubConnection.StopAsync();
            IsConnected = false;
            StatusMessage = "Disconnected from server";
            Items.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect");
            StatusMessage = $"Disconnect failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadItemsAsync()
    {
        try
        {
            var items = await _hubConnection.GetItemsAsync();
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(ItemViewModel.FromDto(item));
            }
            StatusMessage = $"Loaded {Items.Count} items";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load items");
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateItemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            StatusMessage = "Item name is required";
            return;
        }

        try
        {
            var dto = new CreateItemDto(NewItemName, NewItemDescription);
            var idempotencyKey = _idempotencyKeyService.GenerateKey();
            
            await _hubConnection.CreateItemAsync(dto, idempotencyKey);
            
            NewItemName = string.Empty;
            NewItemDescription = string.Empty;
            StatusMessage = "Item created successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create item");
            StatusMessage = $"Create failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateItemAsync()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to update";
            return;
        }

        try
        {
            var dto = SelectedItem.ToUpdateDto();
            var idempotencyKey = _idempotencyKeyService.GenerateKey();
            
            await _hubConnection.UpdateItemAsync(dto, idempotencyKey);
            StatusMessage = "Item updated successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update item");
            StatusMessage = $"Update failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedItem == null)
        {
            StatusMessage = "Please select an item to delete";
            return;
        }

        try
        {
            var idempotencyKey = _idempotencyKeyService.GenerateKey();
            await _hubConnection.DeleteItemAsync(SelectedItem.Id, idempotencyKey);
            StatusMessage = "Item deleted successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete item");
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await DisconnectAsync();
        await _authService.LogoutAsync();
        IsAuthenticated = false;
        Username = null;
        StatusMessage = "Logged out";
        
        // Close main window to trigger app shutdown or login window
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Close();
        }
    }

    private async void OnItemCreated(object? sender, ItemDto item)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Items.Add(ItemViewModel.FromDto(item));
            StatusMessage = $"New item received: {item.Name}";
        });
    }

    private async void OnItemUpdated(object? sender, ItemDto item)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = Items.FirstOrDefault(i => i.Id == item.Id);
            if (existing != null)
            {
                existing.Name = item.Name;
                existing.Description = item.Description;
                existing.UpdatedAt = item.UpdatedAt;
                StatusMessage = $"Item updated: {item.Name}";
            }
        });
    }

    private async void OnItemDeleted(object? sender, Guid itemId)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existing = Items.FirstOrDefault(i => i.Id == itemId);
            if (existing != null)
            {
                Items.Remove(existing);
                StatusMessage = $"Item deleted: {existing.Name}";
            }
        });
    }

    private async void OnNotificationReceived(object? sender, NotificationDto notification)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage = $"Notification: {notification.Message}";
        });
    }

    private async void OnErrorReceived(object? sender, ErrorDto error)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage = $"Error: {error.Message}";
            _error.ShowError(error.Message, error.Code, traceId: null);
        });
    }

    private async void OnRateLimitExceeded(object? sender, RateLimitInfo info)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var secondsUntilReset = (int)(info.ResetTime - DateTime.UtcNow).TotalSeconds;
            _error.ShowRateLimitWarning(Math.Max(secondsUntilReset, 1));
        });
    }
}
