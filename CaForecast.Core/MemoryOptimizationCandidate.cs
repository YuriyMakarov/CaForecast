namespace CaForecast.Core;

public sealed class MemoryOptimizationCandidate
{
    public int MemoryDepthM { get; init; }

    public ForecastErrorMetrics Metrics { get; init; } = new();
}
