using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaForecast.Data;

public sealed class AcademyTopDbContextFactory : IDesignTimeDbContextFactory<AcademyTopDbContext>
{
    public AcademyTopDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CAFORECAST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ca_forecast;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AcademyTopDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AcademyTopDbContext(optionsBuilder.Options);
    }
}
