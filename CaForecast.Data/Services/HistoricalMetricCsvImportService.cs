using System.Globalization;
using CaForecast.Data.Entities;
using CaForecast.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data.Services;

public sealed class HistoricalMetricCsvImportService(IDbContextFactory<AcademyTopDbContext> dbContextFactory)
{
    public async Task<CsvImportResult> ImportAsync(
        string filePath,
        CsvImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV-файл не найден.", filePath);
        }

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException("CSV-файл пуст.");
        }

        var delimiter = DetectDelimiter(lines[0]);
        var firstColumns = SplitLine(lines[0], delimiter);
        var hasHeader = LooksLikeHeader(firstColumns);
        var directionName = options.DirectionNameOverride?.Trim();
        if (string.IsNullOrWhiteSpace(directionName))
        {
            directionName = Path.GetFileNameWithoutExtension(filePath)?.Trim();
        }

        var dataRows = new List<HistoricalMetricRecord>();
        var skippedRows = 0;

        int directionIndex;
        int dateIndex;
        int valueIndex;

        if (hasHeader)
        {
            directionIndex = FindHeaderIndex(firstColumns, ["direction", "course_direction", "name", "metric_name"]);
            dateIndex = FindHeaderIndex(firstColumns, ["metric_date", "date", "period"]);
            valueIndex = FindHeaderIndex(firstColumns, ["metric_value", "value", "sales", "amount"]);
        }
        else
        {
            directionIndex = -1;
            dateIndex = 0;
            valueIndex = 1;
        }

        if (dateIndex < 0 || valueIndex < 0)
        {
            throw new InvalidOperationException("CSV должен содержать столбцы даты и значения метрики.");
        }

        var startIndex = hasHeader ? 1 : 0;
        for (var rowIndex = startIndex; rowIndex < lines.Length; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawLine = lines[rowIndex];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                skippedRows++;
                continue;
            }

            var columns = SplitLine(rawLine, delimiter);
            if (columns.Length <= Math.Max(directionIndex, Math.Max(dateIndex, valueIndex)))
            {
                skippedRows++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(directionName) && directionIndex >= 0)
            {
                directionName = columns[directionIndex].Trim();
            }

            if (!TryParseDate(columns[dateIndex], out var metricDate) ||
                !TryParseDouble(columns[valueIndex], out var metricValue) ||
                metricValue <= 0)
            {
                skippedRows++;
                continue;
            }

            dataRows.Add(new HistoricalMetricRecord
            {
                MetricDate = metricDate,
                MetricValue = metricValue
            });
        }

        if (dataRows.Count == 0)
        {
            throw new InvalidOperationException("В CSV не найдено валидных строк для импорта.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var direction = await dbContext.CourseDirections
            .FirstOrDefaultAsync(x => x.Name == directionName, cancellationToken);

        if (direction is null)
        {
            direction = new CourseDirection
            {
                Name = directionName,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.CourseDirections.Add(direction);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (options.ReplaceExistingSeries)
        {
            var existingRows = dbContext.HistoricalMetrics.Where(x => x.DirectionId == direction.Id);
            dbContext.HistoricalMetrics.RemoveRange(existingRows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var importedRows = 0;
        foreach (var record in dataRows
                     .GroupBy(x => x.MetricDate)
                     .Select(group => group.OrderByDescending(x => x.MetricValue).First())
                     .OrderBy(x => x.MetricDate))
        {
            var existing = await dbContext.HistoricalMetrics
                .FirstOrDefaultAsync(
                    x => x.DirectionId == direction.Id && x.MetricDate == record.MetricDate,
                    cancellationToken);

            if (existing is null)
            {
                dbContext.HistoricalMetrics.Add(new HistoricalMetric
                {
                    DirectionId = direction.Id,
                    MetricDate = record.MetricDate,
                    MetricValue = record.MetricValue
                });
            }
            else
            {
                existing.MetricValue = record.MetricValue;
            }

            importedRows++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CsvImportResult
        {
            DirectionId = direction.Id,
            DirectionName = direction.Name,
            ImportedRows = importedRows,
            SkippedRows = skippedRows
        };
    }

    private static char DetectDelimiter(string line)
    {
        return line.Count(x => x == ';') >= line.Count(x => x == ',') ? ';' : ',';
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter).Select(x => x.Trim().Trim('"')).ToArray();
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> columns)
    {
        return columns.Any(column => column.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                                     column.Contains("direction", StringComparison.OrdinalIgnoreCase) ||
                                     column.Contains("value", StringComparison.OrdinalIgnoreCase));
    }

    private static int FindHeaderIndex(IReadOnlyList<string> headers, IReadOnlyList<string> candidates)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = headers[index].Trim().ToLowerInvariant();
            if (candidates.Contains(normalized))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateOnly.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDouble(string value, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }
}
