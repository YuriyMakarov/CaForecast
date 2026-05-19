namespace CaForecast.Core;

public sealed class ForecastPoint
{
    public DateOnly? Period { get; init; }

    public double ActualValue { get; init; }

    public double PredictedValue { get; init; }
}
