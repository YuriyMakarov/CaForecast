using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data;

public sealed class RuntimeDbContextFactory(DbContextOptions<AcademyTopDbContext> options)
    : IDbContextFactory<AcademyTopDbContext>
{
    public AcademyTopDbContext CreateDbContext()
    {
        return new AcademyTopDbContext(options);
    }

    public Task<AcademyTopDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AcademyTopDbContext(options));
    }
}
