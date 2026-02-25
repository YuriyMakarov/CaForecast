namespace CaForecast.Data;

public sealed class MemoryMetricCsvRow
{
    public required int Memory { get; init; }

    public required double Mae { get; init; }

    public required double Mse { get; init; }

    public required double Rmse { get; init; }

    public required double Mape { get; init; }
}
