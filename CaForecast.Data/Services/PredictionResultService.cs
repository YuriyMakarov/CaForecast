using CaForecast.Data.Entities;
using CaForecast.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data.Services;

public sealed class PredictionResultService(IDbContextFactory<AcademyTopDbContext> dbContextFactory)
{
    public async Task SaveAsync(ExperimentLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var setting = new ModelSetting
        {
            MemoryDepthM = entry.MemoryDepthM,
            ThresholdK = entry.ThresholdK,
            SmoothingAlpha = entry.SmoothingAlpha
        };

        dbContext.ModelSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.PredictionResults.Add(new PredictionResult
        {
            SettingId = setting.Id,
            DirectionId = entry.DirectionId,
            CalculatedAt = DateTime.UtcNow,
            Mae = entry.Mae,
            Rmse = entry.Rmse,
            Mape = entry.Mape,
            PredictedValuesJson = entry.PredictedValuesJson
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
