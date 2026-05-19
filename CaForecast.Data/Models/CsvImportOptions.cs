namespace CaForecast.Data.Models;

public sealed class CsvImportOptions
{
    public string? DirectionNameOverride { get; init; }

    public bool ReplaceExistingSeries { get; init; }
}
