using System.IO;
using System.Windows;
using CaForecast.Data;
using CaForecast.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CaForecast.WpfApp;

public partial class App : Application
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
            ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "ca_forecast.db")}";

        var optionsBuilder = new DbContextOptionsBuilder<AcademyTopDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        var dbContextFactory = new RuntimeDbContextFactory(optionsBuilder.Options);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var viewModel = new MainViewModel(
            new HistoricalMetricCsvImportService(dbContextFactory),
            new DirectionQueryService(dbContextFactory),
            new PredictionResultService(dbContextFactory),
            new StoredSeriesManagementService(dbContextFactory));

        await viewModel.InitializeAsync();

        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
