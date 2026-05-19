namespace CaForecast.Data.Models;

public sealed class ExperimentLogEntry
{
    public int DirectionId { get; init; }

    public int MemoryDepthM { get; init; }

    public double ThresholdK { get; init; }

    public double SmoothingAlpha { get; init; }

    public double Mae { get; init; }

    public double Rmse { get; init; }

    public double Mape { get; init; }

    public string PredictedValuesJson { get; init; } = "[]";
}
