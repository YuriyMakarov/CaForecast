using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaForecast.Data;

public sealed class AcademyTopDbContextFactory : IDesignTimeDbContextFactory<AcademyTopDbContext>
{
    public AcademyTopDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CAFORECAST_CONNECTION")
            ?? "Data Source=ca_forecast.db";

        var optionsBuilder = new DbContextOptionsBuilder<AcademyTopDbContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new AcademyTopDbContext(optionsBuilder.Options);
    }
}
