namespace CaForecast.Core;

public sealed class MemoryOptimizationResult
{
    public int BestMemoryDepthM { get; init; }

    public ForecastScenarioResult BestScenario { get; init; } = new();

    public IReadOnlyList<MemoryOptimizationCandidate> Candidates { get; init; } = Array.Empty<MemoryOptimizationCandidate>();
}
