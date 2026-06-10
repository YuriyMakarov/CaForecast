using CaForecast.Data.Services;

namespace CaForecast.WpfApp.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthenticationService authenticationService;
    private readonly AsyncRelayCommand loginCommand;
    private string login = string.Empty;
    private string password = string.Empty;
    private string errorMessage = string.Empty;
    private bool isBusy;

    public LoginViewModel(AuthenticationService authenticationService)
    {
        this.authenticationService = authenticationService;
        loginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
    }

    public event EventHandler<AuthenticatedUser>? LoginSucceeded;

    public string Login
    {
        get => login;
        set
        {
            if (SetProperty(ref login, value))
            {
                loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => password;
        set
        {
            if (SetProperty(ref password, value))
            {
                loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand LoginCommand => loginCommand;

    private bool CanLogin()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Login)
            && !string.IsNullOrEmpty(Password);
    }

    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var user = await authenticationService.AuthenticateAsync(Login, Password);
            if (user is null)
            {
                ErrorMessage = "Неверный логин или пароль.";
                return;
            }

            LoginSucceeded?.Invoke(this, user);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            ErrorMessage = "Не удалось выполнить вход. Проверьте подключение к базе данных.";
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }
}
