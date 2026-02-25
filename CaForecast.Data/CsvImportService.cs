using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CaForecast.Data;

public class CsvImportService
{
    public CsvImportedData Import(string filePath, CsvSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Требуется путь к файлу.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV-файл не найден.", filePath);
        }

        settings ??= new CsvSettings();

        using var reader = new StreamReader(filePath);
        var sampledLines = new List<(int Number, string Text)>(10);
        var lineNumber = 0;

        while (sampledLines.Count < 10)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            sampledLines.Add((lineNumber, line));
        }

        if (sampledLines.Count == 0)
        {
            throw new InvalidOperationException("CSV-файл пуст.");
        }

        var delimiter = settings.Delimiter ?? DetectDelimiter(sampledLines.Select(l => l.Text).ToArray());

        var closeIndex = 0;
        var dateIndex = -1;
        var sampledDataStartIndex = 0;

        if (settings.HasHeader)
        {
            var headerColumns = SplitLine(sampledLines[0].Text, delimiter);
            var parsed = ParseHeader(headerColumns);

            if (parsed.HasExplicitClose)
            {
                closeIndex = parsed.CloseIndex;
                dateIndex = parsed.DateIndex;
            }
            else
            {
                string[] firstDataColumns;
                if (sampledLines.Count > 1)
                {
                    firstDataColumns = SplitLine(sampledLines[1].Text, delimiter);
                }
                else
                {
                    firstDataColumns = ReadNextNonEmptyColumns(reader, delimiter, sampledLines, ref lineNumber);
                }

                if (firstDataColumns.Length >= 2 &&
                    TryParseDouble(GetColumn(firstDataColumns, 1), out _))
                {
                    dateIndex = 0;
                    closeIndex = 1;
                }
                else
                {
                    dateIndex = -1;
                    closeIndex = 0;
                }
            }

            sampledDataStartIndex = 1;
        }
        else
        {
            var firstDataCols = SplitLine(sampledLines[0].Text, delimiter);
            if (firstDataCols.Length > 1)
            {
                dateIndex = 0;
                closeIndex = 1;
            }
        }

        var prices = new List<double>();
        var dates = new List<DateTime?>();

        for (var i = sampledDataStartIndex; i < sampledLines.Count; i++)
        {
            if (IsEndOfDataMarker(sampledLines[i].Text))
            {
                break;
            }

            ParseDataLine(sampledLines[i].Text, sampledLines[i].Number, delimiter, closeIndex, dateIndex, prices, dates);
        }

        while (true)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsEndOfDataMarker(line))
            {
                break;
            }

            ParseDataLine(line, lineNumber, delimiter, closeIndex, dateIndex, prices, dates);
        }

        if (prices.Count < 2)
        {
            throw new InvalidOperationException("Требуется минимум две цены закрытия.");
        }

        return new CsvImportedData
        {
            ClosePrices = prices,
            Dates = dates
        };
    }

    private static void ParseDataLine(
        string line,
        int lineNumber,
        char delimiter,
        int closeIndex,
        int dateIndex,
        ICollection<double> prices,
        ICollection<DateTime?> dates)
    {
        var columns = SplitLine(line, delimiter);
        if (columns.Length == 0)
        {
            return;
        }

        var closeValue = GetColumn(columns, closeIndex);
        if (!TryParseDouble(closeValue, out var close))
        {
            if (IsIgnorableNonDataLine(line, columns, closeIndex, closeValue))
            {
                return;
            }

            throw new FormatException($"Не удалось разобрать значение Close в строке {lineNumber}: '{closeValue}'.");
        }

        prices.Add(close);
        dates.Add(dateIndex >= 0 ? ParseDateSafe(GetColumn(columns, dateIndex)) : null);
    }

    private static string[] ReadNextNonEmptyColumns(
        TextReader reader,
        char delimiter,
        ICollection<(int Number, string Text)> sampledLines,
        ref int lineNumber)
    {
        while (true)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                return Array.Empty<string>();
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsEndOfDataMarker(line))
            {
                return Array.Empty<string>();
            }

            sampledLines.Add((lineNumber, line));
            return SplitLine(line, delimiter);
        }
    }

    private static bool IsEndOfDataMarker(string line)
    {
        return line.Trim().StartsWith("history.cursor", StringComparison.OrdinalIgnoreCase);
    }

    private static (int CloseIndex, int DateIndex, bool HasExplicitClose) ParseHeader(IReadOnlyList<string> headerColumns)
    {
        var closeIndex = -1;
        var dateIndex = -1;

        for (var i = 0; i < headerColumns.Count; i++)
        {
            var name = headerColumns[i].Trim();
            if (name.Equals("Close", StringComparison.OrdinalIgnoreCase))
            {
                closeIndex = i;
            }
            else if (name.Equals("Date", StringComparison.OrdinalIgnoreCase))
            {
                dateIndex = i;
            }
        }

        if (closeIndex < 0)
        {
            if (headerColumns.Count == 1)
            {
                closeIndex = 0;
            }
            else if (headerColumns.Count >= 2)
            {
                closeIndex = 1;
                dateIndex = 0;
            }
            else
            {
                throw new InvalidOperationException("Заголовок CSV не содержит столбец Close.");
            }
        }

        return (closeIndex, dateIndex, closeIndex >= 0 && headerColumns.Any(h => h.Trim().Equals("Close", StringComparison.OrdinalIgnoreCase)));
    }

    private static char DetectDelimiter(IReadOnlyList<string> lines)
    {
        var commaCount = 0;
        var semicolonCount = 0;

        foreach (var line in lines.Take(10))
        {
            commaCount += line.Count(c => c == ',');
            semicolonCount += line.Count(c => c == ';');
        }

        return semicolonCount > commaCount ? ';' : ',';
    }

    private static string[] SplitLine(string line, char delimiter)
    {
        return line.Split(delimiter);
    }

    private static bool IsIgnorableNonDataLine(string rawLine, IReadOnlyList<string> columns, int closeIndex, string closeValue)
    {
        var text = rawLine.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (text.StartsWith('#'))
        {
            return true;
        }

        if (columns.Count <= closeIndex || string.IsNullOrWhiteSpace(closeValue))
        {
            return !text.Any(char.IsDigit);
        }

        return false;
    }

    private static string GetColumn(IReadOnlyList<string> columns, int index)
    {
        if (index < 0 || index >= columns.Count)
        {
            return string.Empty;
        }

        return columns[index].Trim();
    }

    private static bool TryParseDouble(string input, out double value)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static DateTime? ParseDateSafe(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
        {
            return invariant;
        }

        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.None, out var current))
        {
            return current;
        }

        return null;
    }
}
