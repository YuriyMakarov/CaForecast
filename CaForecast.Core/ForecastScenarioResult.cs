namespace CaForecast.Core;

public sealed class ForecastScenarioResult
{
    public ForecastingParameters Parameters { get; init; } = new();

    public int TrainingObservationCount { get; init; }

    public int FallbackUsageCount { get; init; }

    public ForecastErrorMetrics Metrics { get; init; } = new();

    public IReadOnlyList<double> ActualValues { get; init; } = Array.Empty<double>();

    public IReadOnlyList<double> PredictedValues { get; init; } = Array.Empty<double>();

    public IReadOnlyList<double> ActualReturns { get; init; } = Array.Empty<double>();

    public IReadOnlyList<double> PredictedReturns { get; init; } = Array.Empty<double>();

    public IReadOnlyList<ForecastPoint> ForecastPoints { get; init; } = Array.Empty<ForecastPoint>();
}
