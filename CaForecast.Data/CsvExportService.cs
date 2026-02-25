using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CaForecast.Data;

public class CsvExportService
{
    private static readonly CultureInfo ExportCulture = CultureInfo.CurrentCulture;

    public void ExportMetrics(string filePath, IEnumerable<MemoryMetricCsvRow> rows, char delimiter = ';')
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Требуется путь к файлу.", nameof(filePath));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(delimiter, "Память", "MAE", "MSE", "RMSE", "MAPE"));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(
                delimiter,
                row.Memory.ToString(ExportCulture),
                row.Mae.ToString(ExportCulture),
                row.Mse.ToString(ExportCulture),
                row.Rmse.ToString(ExportCulture),
                row.Mape.ToString(ExportCulture)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public void ExportForecast(string filePath, IEnumerable<ForecastPointCsvRow> rows, char delimiter = ';')
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Требуется путь к файлу.", nameof(filePath));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(delimiter, "Дата", "Изначальная цена", "Прогнозная цена"));

        foreach (var row in rows)
        {
            var dateText = row.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            sb.AppendLine(string.Join(
                delimiter,
                dateText,
                row.ActualPrice.ToString(ExportCulture),
                row.PredictedPrice.ToString(ExportCulture)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public void ExportCombined(
        string filePath,
        IEnumerable<MemoryMetricCsvRow> metricRows,
        IEnumerable<ForecastPointCsvRow> forecastRows,
        int bestMemory,
        char delimiter = ';')
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Требуется путь к файлу.", nameof(filePath));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"ЛучшаяПамять{delimiter}{bestMemory}");
        sb.AppendLine();
        sb.AppendLine("Метрики");
        sb.AppendLine(string.Join(delimiter, "Память", "MAE", "MSE", "RMSE", "MAPE"));

        foreach (var row in metricRows)
        {
            sb.AppendLine(string.Join(
                delimiter,
                row.Memory.ToString(ExportCulture),
                row.Mae.ToString(ExportCulture),
                row.Mse.ToString(ExportCulture),
                row.Rmse.ToString(ExportCulture),
                row.Mape.ToString(ExportCulture)));
        }

        sb.AppendLine();
        sb.AppendLine("Прогнозная цена");
        sb.AppendLine(string.Join(delimiter, "Дата", "Изначальная цена", "Прогнозная цена"));

        foreach (var row in forecastRows)
        {
            var dateText = row.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            sb.AppendLine(string.Join(
                delimiter,
                dateText,
                row.ActualPrice.ToString(ExportCulture),
                row.PredictedPrice.ToString(ExportCulture)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}

