using CaForecast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data.Services;

public sealed class DirectionQueryService(IDbContextFactory<AcademyTopDbContext> dbContextFactory)
{
    public async Task<IReadOnlyList<CourseDirection>> GetDirectionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CourseDirections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(DateOnly Date, double Value)>> GetSeriesAsync(int directionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.HistoricalMetrics
            .AsNoTracking()
            .Where(x => x.DirectionId == directionId)
            .OrderBy(x => x.MetricDate)
            .Select(x => new ValueTuple<DateOnly, double>(x.MetricDate, x.MetricValue))
            .ToListAsync(cancellationToken);
    }
}
