using Avalonia.Controls;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Views;

public partial class LoginView : Window
{
    public LoginView()
    {
        InitializeComponent();
        
        // Close window when login succeeds
        DataContextChanged += (s, e) =>
        {
            if (DataContext is LoginViewModel loginViewModel)
            {
                loginViewModel.LoginSucceeded += (s, e) => Close();
            }
        };
    }
}
