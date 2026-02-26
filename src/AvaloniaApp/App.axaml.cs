using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views;
using AvaloniaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AvaloniaApp;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Check if user is authenticated, show login or main window
            var authService = Services.GetRequiredService<IAuthService>();
            
            if (!authService.IsAuthenticated)
            {
                var loginViewModel = Services.GetRequiredService<LoginViewModel>();
                var loginWindow = new LoginView
                {
                    DataContext = loginViewModel
                };
                
                loginWindow.Closed += (s, e) =>
                {
                    if (authService.IsAuthenticated)
                    {
                        ShowMainWindow();
                    }
                    else
                    {
                        desktop.Shutdown();
                    }
                };
                
                desktop.MainWindow = loginWindow;
            }
            else
            {
                ShowMainWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = Services!.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            
            // Handle main window closing (e.g., logout)
            mainWindow.Closed += (s, e) =>
            {
                var authService = Services!.GetRequiredService<IAuthService>();
                if (!authService.IsAuthenticated)
                {
                    // User logged out, show login window again
                    var loginViewModel = Services.GetRequiredService<LoginViewModel>();
                    var loginWindow = new LoginView
                    {
                        DataContext = loginViewModel
                    };
                    
                    loginWindow.Closed += (s2, e2) =>
                    {
                        if (authService.IsAuthenticated)
                        {
                            ShowMainWindow();
                        }
                        else
                        {
                            desktop.Shutdown();
                        }
                    };
                    
                    desktop.MainWindow = loginWindow;
                    loginWindow.Show();
                }
            };
            
            desktop.MainWindow = mainWindow;
            desktop.MainWindow.Show();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Services
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IHubConnectionService, HubConnectionService>();
        services.AddSingleton<IIdempotencyKeyService, IdempotencyKeyService>();
        services.AddSingleton<IHubOperationPolicy, HubOperationPolicy>();

        // ViewModels
        services.AddSingleton<ErrorViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}