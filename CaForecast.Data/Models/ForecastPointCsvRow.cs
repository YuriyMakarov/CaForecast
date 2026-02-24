namespace CaForecast.Data;

public sealed class ForecastPointCsvRow
{
    public required DateTime? Date { get; init; }

    public required double ActualPrice { get; init; }

    public required double PredictedPrice { get; init; }
}
