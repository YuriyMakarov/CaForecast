using System.Windows;
using CaForecast.Data;
using CaForecast.Data.Services;
using CaForecast.WpfApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Role = CaForecast.Data.Entities.Role;
using User = CaForecast.Data.Entities.User;

namespace CaForecast.WpfApp;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var connectionString =
            configuration.GetConnectionString("AcademyTop")
            ?? "Host=localhost;Port=5432;Database=ca_forecast;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AcademyTopDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        var dbContextFactory = new RuntimeDbContextFactory(optionsBuilder.Options);
        var passwordHashService = new PasswordHashService();

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await SeedSecurityDataAsync(dbContext, passwordHashService);
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Не удалось подключиться к PostgreSQL. Проверьте строку подключения и доступность сервера.",
                "Ошибка подключения к базе данных",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var authenticationService = new AuthenticationService(dbContextFactory, passwordHashService);
        var loginWindow = new LoginWindow(new LoginViewModel(authenticationService));

        if (loginWindow.ShowDialog() != true || loginWindow.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        var workspace = CreateWorkspaceWindow(loginWindow.AuthenticatedUser);
        MainWindow = workspace;
        workspace.Show();
    }

    private static Window CreateWorkspaceWindow(AuthenticatedUser user)
    {
        var normalizedRole = user.RoleName.Trim().ToLowerInvariant();

        return normalizedRole switch
        {
            "manager" or "менеджер" => new ManagerWindow(user),
            "director" or "директор" => new DirectorWindow(user),
            _ => throw new InvalidOperationException($"Неизвестная роль пользователя: {user.RoleName}.")
        };
    }

    private static async Task SeedSecurityDataAsync(AcademyTopDbContext dbContext, PasswordHashService passwordHashService)
    {
        var managerRole = await GetOrCreateRoleAsync(dbContext, "Manager");
        var directorRole = await GetOrCreateRoleAsync(dbContext, "Director");

        await CreateUserIfMissingAsync(
            dbContext,
            managerRole,
            "manager",
            "manager123",
            "Менеджер приемной комиссии",
            passwordHashService);

        await CreateUserIfMissingAsync(
            dbContext,
            directorRole,
            "director",
            "director123",
            "Директор колледжа",
            passwordHashService);

        await dbContext.SaveChangesAsync();
    }

    private static async Task<Role> GetOrCreateRoleAsync(AcademyTopDbContext dbContext, string name)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.Name == name);
        if (role is not null)
        {
            return role;
        }

        role = new Role { Name = name };
        dbContext.Roles.Add(role);
        return role;
    }

    private static async Task CreateUserIfMissingAsync(
        AcademyTopDbContext dbContext,
        Role role,
        string login,
        string password,
        string fullName,
        PasswordHashService passwordHashService)
    {
        if (await dbContext.Users.AnyAsync(x => x.Login == login))
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Role = role,
            Login = login,
            PasswordHash = passwordHashService.HashPassword(password),
            FullName = fullName,
            IsActive = true
        });
    }
}
