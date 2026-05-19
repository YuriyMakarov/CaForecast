namespace CaForecast.Core;

public sealed class ForecastErrorMetrics
{
    public double Mae { get; init; }

    public double Mse { get; init; }

    public double Rmse { get; init; }

    public double Mape { get; init; }
}
