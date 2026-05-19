namespace CaForecast.Data.Models;

public sealed class HistoricalMetricRecord
{
    public DateOnly MetricDate { get; init; }

    public double MetricValue { get; init; }
}
