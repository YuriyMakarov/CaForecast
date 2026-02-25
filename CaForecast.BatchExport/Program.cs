using System.Globalization;
using System.Text;
using CaForecast.Core;
using CaForecast.Data;

var root = Directory.GetCurrentDirectory();
var inputDir = Path.Combine(root, "RealData");
var outputDir = Path.Combine(root, "Result");

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Папка не найдена: {inputDir}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var csvImport = new CsvImportService();
var csvExport = new CsvExportService();
var returnCalculator = new ReturnCalculator();
var encoder = new ThreeColorEncoder();
var trainer = new CaRuleTrainer();
var forecaster = new CaForecaster();
var metricsService = new MetricsService();

const double trainPercent = 70.0;
const double k = 0.002;
const double alpha = 1.0;
const int maxMemoryM = 8;
const char delimiter = ';';

var summaries = new List<SummaryRow>();
var files = Directory.GetFiles(inputDir, "*.csv", SearchOption.TopDirectoryOnly)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

foreach (var file in files)
{
    var imported = csvImport.Import(file, new CsvSettings { Delimiter = ';', HasHeader = true });
    var prices = imported.ClosePrices;
    var dates = imported.Dates;
    var fileBase = Path.GetFileNameWithoutExtension(file);

    if (fileBase.Equals("GMKN", StringComparison.OrdinalIgnoreCase))
    {
        var cutIndex = FindPreSplitCutIndex(prices);
        if (cutIndex > 1 && cutIndex < prices.Count - 1)
        {
            prices = prices.Take(cutIndex + 1).ToList();
            dates = dates.Take(cutIndex + 1).ToList();
            Console.WriteLine($"GMKN pre-split mode: using data up to {dates[^1]:yyyy-MM-dd} (rows={prices.Count}).");
        }
    }

    var returns = returnCalculator.CalculateLogReturns(prices);
    var encodedStates = encoder.Encode(returns, k);

    var trainReturnsCount = (int)Math.Round(returns.Count * (trainPercent / 100.0), MidpointRounding.AwayFromZero);
    trainReturnsCount = Math.Max(2, Math.Min(trainReturnsCount, returns.Count - 1));

    var metricRows = new List<MemoryMetricCsvRow>();
    ForecastResult? bestResult = null;

    for (var m = 1; m <= maxMemoryM; m++)
    {
        if (m >= trainReturnsCount)
        {
            break;
        }

        var result = forecaster.Forecast(
            prices,
            returns,
            encodedStates,
            trainReturnsCount,
            m,
            alpha,
            trainer,
            metricsService);

        metricRows.Add(new MemoryMetricCsvRow
        {
            Memory = result.Memory,
            Mae = result.Mae,
            Mse = result.Mse,
            Rmse = result.Rmse,
            Mape = result.MapePercent / 100.0
        });

        if (bestResult is null || result.Rmse < bestResult.Rmse)
        {
            bestResult = result;
        }
    }

    if (bestResult is null)
    {
        throw new InvalidOperationException($"Не удалось получить прогноз для файла {Path.GetFileName(file)}.");
    }

    var bestDates = BuildBestDates(dates, bestResult.TrainReturnsCount, bestResult.ActualPrices.Count);
    var forecastRows = bestResult.ActualPrices.Select((actual, i) => new ForecastPointCsvRow
    {
        Date = i < bestDates.Count ? bestDates[i] : null,
        ActualPrice = actual,
        PredictedPrice = bestResult.PredictedPrices[i]
    }).ToArray();

    var exportPath = Path.Combine(outputDir, $"{fileBase}_export.csv");
    csvExport.ExportCombined(exportPath, metricRows, forecastRows, bestResult.Memory, delimiter);

    var maePercent = CalculateRelativePercent(bestResult.Mae, bestResult.ActualPrices);
    var msePercent = CalculateRelativeSquaredPercent(bestResult.Mse, bestResult.ActualPrices);
    var rmsePercent = CalculateRelativePercent(bestResult.Rmse, bestResult.ActualPrices);

    summaries.Add(new SummaryRow(
        fileBase,
        bestResult.Memory,
        bestResult.Mae,
        bestResult.Mse,
        bestResult.Rmse,
        maePercent,
        msePercent,
        rmsePercent,
        bestResult.MapePercent));

    Console.WriteLine($"OK: {Path.GetFileName(exportPath)}");
}

var summaryPath = Path.Combine(outputDir, "summary_comparison.csv");
WriteSummary(summaryPath, summaries, delimiter);
Console.WriteLine($"OK: {Path.GetFileName(summaryPath)}");
return 0;

static int FindPreSplitCutIndex(IReadOnlyList<double> prices)
{
    // Cut the series right before the largest absolute log-return jump (split-like discontinuity).
    var maxAbsRet = 0.0;
    var jumpIndex = -1;
    for (var i = 1; i < prices.Count; i++)
    {
        var prev = prices[i - 1];
        var curr = prices[i];
        if (prev <= 0 || curr <= 0)
        {
            continue;
        }

        var absRet = Math.Abs(Math.Log(curr / prev));
        if (absRet > maxAbsRet)
        {
            maxAbsRet = absRet;
            jumpIndex = i;
        }
    }

    return jumpIndex > 1 ? jumpIndex - 1 : prices.Count - 1;
}

static List<DateTime?> BuildBestDates(IReadOnlyList<DateTime?> allDates, int trainReturnsCount, int forecastCount)
{
    var result = new List<DateTime?>(forecastCount);
    for (var i = 0; i < forecastCount; i++)
    {
        var dateIndex = trainReturnsCount + 1 + i;
        result.Add(dateIndex >= 0 && dateIndex < allDates.Count ? allDates[dateIndex] : null);
    }

    return result;
}

static double CalculateRelativePercent(double metricValue, IReadOnlyList<double> actualPrices)
{
    if (actualPrices.Count == 0)
    {
        return double.NaN;
    }

    var meanAbsActual = actualPrices.Select(Math.Abs).Average();
    if (meanAbsActual < 1e-12)
    {
        return double.NaN;
    }

    return (metricValue / meanAbsActual) * 100.0;
}

static double CalculateRelativeSquaredPercent(double metricValue, IReadOnlyList<double> actualPrices)
{
    if (actualPrices.Count == 0)
    {
        return double.NaN;
    }

    var meanSquareActual = actualPrices.Select(v => v * v).Average();
    if (meanSquareActual < 1e-12)
    {
        return double.NaN;
    }

    return (metricValue / meanSquareActual) * 100.0;
}

static void WriteSummary(string path, IEnumerable<SummaryRow> rows, char delimiter)
{
    var culture = CultureInfo.CurrentCulture;
    var sb = new StringBuilder();
    sb.AppendLine(string.Join(delimiter,
        "Ряд",
        "ЛучшаяПамять_m",
        "MAE",
        "MSE",
        "RMSE",
        "MAE_%",
        "MSE_%",
        "RMSE_%",
        "MAPE_%"));

    foreach (var row in rows)
    {
        sb.AppendLine(string.Join(delimiter,
            row.Series,
            row.BestMemory.ToString(culture),
            row.Mae.ToString(culture),
            row.Mse.ToString(culture),
            row.Rmse.ToString(culture),
            row.MaePercent.ToString(culture),
            row.MsePercent.ToString(culture),
            row.RmsePercent.ToString(culture),
            row.MapePercent.ToString(culture)));
    }

    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
}

internal sealed record SummaryRow(
    string Series,
    int BestMemory,
    double Mae,
    double Mse,
    double Rmse,
    double MaePercent,
    double MsePercent,
    double RmsePercent,
    double MapePercent);
