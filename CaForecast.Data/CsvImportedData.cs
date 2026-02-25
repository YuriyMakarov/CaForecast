namespace CaForecast.Data;

public sealed class CsvImportedData
{
    public required IReadOnlyList<double> ClosePrices { get; init; }

    public required IReadOnlyList<DateTime?> Dates { get; init; }
}
