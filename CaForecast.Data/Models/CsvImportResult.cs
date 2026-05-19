namespace CaForecast.Data.Models;

public sealed class CsvImportResult
{
    public int DirectionId { get; init; }

    public string DirectionName { get; init; } = string.Empty;

    public int ImportedRows { get; init; }

    public int SkippedRows { get; init; }
}
