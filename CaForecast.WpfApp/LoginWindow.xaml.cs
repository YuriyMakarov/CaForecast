using System.Windows;
using CaForecast.Data.Services;
using CaForecast.WpfApp.ViewModels;

namespace CaForecast.WpfApp;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    public AuthenticatedUser? AuthenticatedUser { get; private set; }

    private void OnLoginSucceeded(object? sender, AuthenticatedUser user)
    {
        AuthenticatedUser = user;
        DialogResult = true;
        Close();
    }
}
