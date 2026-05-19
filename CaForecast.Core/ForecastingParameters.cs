namespace CaForecast.Core;

public sealed class ForecastingParameters
{
    public int MemoryDepthM { get; init; }

    public double ThresholdK { get; init; }

    public double SmoothingAlpha { get; init; }

    /// <summary>
    /// Доля обучающей выборки в диапазоне (0; 1).
    /// </summary>
    public double TrainRatio { get; init; } = 0.8;
}
