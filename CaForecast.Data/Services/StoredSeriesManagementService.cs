using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data.Services;

public sealed class StoredSeriesManagementService(IDbContextFactory<AcademyTopDbContext> dbContextFactory)
{
    public async Task DeleteDirectionAsync(int directionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var direction = await dbContext.CourseDirections
            .FirstOrDefaultAsync(x => x.Id == directionId, cancellationToken);

        if (direction is null)
        {
            return;
        }

        dbContext.CourseDirections.Remove(direction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveOrphanedModelSettingsAsync(dbContext, cancellationToken);
    }

    public async Task DeleteAllDirectionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var directions = await dbContext.CourseDirections.ToListAsync(cancellationToken);
        if (directions.Count == 0)
        {
            return;
        }

        dbContext.CourseDirections.RemoveRange(directions);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveOrphanedModelSettingsAsync(dbContext, cancellationToken);
    }

    private static async Task RemoveOrphanedModelSettingsAsync(
        AcademyTopDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var orphanedSettings = await dbContext.ModelSettings
            .Where(x => !x.PredictionResults.Any())
            .ToListAsync(cancellationToken);

        if (orphanedSettings.Count == 0)
        {
            return;
        }

        dbContext.ModelSettings.RemoveRange(orphanedSettings);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
