using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CaForecast.Core;
using CaForecast.Data;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace CaForecast.WpfApp;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly CsvImportService _csvImportService = new();
    private readonly MoexIssService _moexIssService = new();
    private readonly CsvExportService _csvExportService = new();
    private readonly ReturnCalculator _returnCalculator = new();
    private readonly ThreeColorEncoder _encoder = new();
    private readonly CaRuleTrainer _trainer = new();
    private readonly CaForecaster _forecaster = new();
    private readonly MetricsService _metricsService = new();

    private CsvImportedData? _sourceData;
    private CsvImportedData? _loadedData;
    private ForecastResult? _bestResult;
    private List<DateTime?> _bestDates = new();

    private string _selectedFilePath = "Данные не загружены";
    private bool _isApiInputsVisible;
    private string _secIdText = "SBER";
    private string _boardIdText = "TQBR";
    private string _fromDateText = DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private string _tillDateText = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private string _trainPercentText = "70";
    private string _kText = "0.002";
    private string _alphaText = "1.0";
    private string _maxMemoryMText = "8";
    private string _maxPlotPointsText = "3000";
    private string _statusMessage = "Загрузите CSV или MOEX ISS.";
    private PlotModel _plotModel = BuildEmptyPlotModel();
    private bool _isInternalUpdate;
    private bool _isBusy;
    private readonly CancellationTokenSource _shutdownCts = new();

    public MainViewModel()
    {
        LoadCsvCommand = new RelayCommand(LoadCsv, () => !_isBusy);
        ShowMoexInputsCommand = new RelayCommand(() => IsApiInputsVisible = true);
        HideMoexInputsCommand = new RelayCommand(() => IsApiInputsVisible = false);
        LoadMoexCommand = new RelayCommand(() => _ = LoadMoexAsync(), () => !_isBusy);
        RunCommand = new RelayCommand(() => _ = RunAsync(showErrors: true), () => _loadedData is not null && !_isBusy);
        ExportResultsCommand = new RelayCommand(ExportResults, () => _bestResult is not null && !_isBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand LoadCsvCommand { get; }

    public RelayCommand ShowMoexInputsCommand { get; }

    public RelayCommand HideMoexInputsCommand { get; }

    public RelayCommand LoadMoexCommand { get; }

    public RelayCommand RunCommand { get; }

    public RelayCommand ExportResultsCommand { get; }

    public ObservableCollection<MemoryErrorRow> ErrorRows { get; } = new();

    public ObservableCollection<BestMemoryErrorRow> BestRows { get; } = new();

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => SetField(ref _selectedFilePath, value);
    }

    public bool IsApiInputsVisible
    {
        get => _isApiInputsVisible;
        set => SetField(ref _isApiInputsVisible, value);
    }

    public string SecIdText
    {
        get => _secIdText;
        set => SetField(ref _secIdText, value);
    }

    public string BoardIdText
    {
        get => _boardIdText;
        set => SetField(ref _boardIdText, value);
    }

    public string FromDateText
    {
        get => _fromDateText;
        set
        {
            if (SetField(ref _fromDateText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string TillDateText
    {
        get => _tillDateText;
        set
        {
            if (SetField(ref _tillDateText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string TrainPercentText
    {
        get => _trainPercentText;
        set
        {
            if (SetField(ref _trainPercentText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string KText
    {
        get => _kText;
        set
        {
            if (SetField(ref _kText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string AlphaText
    {
        get => _alphaText;
        set
        {
            if (SetField(ref _alphaText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string MaxMemoryMText
    {
        get => _maxMemoryMText;
        set
        {
            if (SetField(ref _maxMemoryMText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string MaxPlotPointsText
    {
        get => _maxPlotPointsText;
        set
        {
            if (SetField(ref _maxPlotPointsText, value))
            {
                AutoRefresh();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public PlotModel PlotModel
    {
        get => _plotModel;
        set => SetField(ref _plotModel, value);
    }

    public IPlotController LockedPlotController { get; } = BuildLockedPlotController();

    public void CancelBackgroundOperations()
    {
        _shutdownCts.Cancel();
    }

    private void LoadCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV-файлы (*.csv)|*.csv|Все файлы (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _sourceData = _csvImportService.Import(dialog.FileName, new CsvSettings());
            SelectedFilePath = dialog.FileName;
            IsApiInputsVisible = false;
            InitializeDateRangeFromData(_sourceData);
            if (!TryApplyDateFilter(showErrors: true))
            {
                return;
            }

            _ = RunAsync(showErrors: true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки CSV: {ex.Message}";
        }
    }

    private async Task LoadMoexAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            if (!TryParseDate(FromDateText, out var fromDate))
            {
                throw new InvalidOperationException("Дата \"С\" должна быть в формате yyyy-MM-dd.");
            }

            if (!TryParseDate(TillDateText, out var tillDate))
            {
                throw new InvalidOperationException("Дата \"По\" должна быть в формате yyyy-MM-dd.");
            }

            var secId = (SecIdText ?? string.Empty).Trim().ToUpperInvariant();
            var boardId = (BoardIdText ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(secId))
            {
                throw new InvalidOperationException("Укажите тикер бумаги (например, SBER).");
            }

            if (string.IsNullOrWhiteSpace(boardId))
            {
                throw new InvalidOperationException("Укажите режим торгов (например, TQBR).");
            }

            StatusMessage = "Загрузка MOEX ISS...";
            var importedData = await Task.Run(
                () => _moexIssService.ImportDailyHistory(secId, fromDate, tillDate, boardId),
                _shutdownCts.Token);

            if (_shutdownCts.IsCancellationRequested)
            {
                return;
            }

            _sourceData = importedData;
            SelectedFilePath = $"MOEX ISS: {boardId}/{secId}";
            IsApiInputsVisible = false;
            if (!TryApplyDateFilter(showErrors: true))
            {
                return;
            }

            await RunAsync(showErrors: true, manageBusy: false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Операция отменена.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки MOEX ISS: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunAsync(bool showErrors, bool manageBusy = true)
    {
        if (_isBusy && manageBusy)
        {
            return;
        }

        if (manageBusy)
        {
            SetBusy(true);
        }

        if (!TryApplyDateFilter(showErrors: true))
        {
            if (manageBusy)
            {
                SetBusy(false);
            }

            return;
        }

        if (_loadedData is null)
        {
            if (showErrors)
            {
                StatusMessage = "Сначала загрузите данные.";
            }

            if (manageBusy)
            {
                SetBusy(false);
            }

            return;
        }

        try
        {
            if (!TryParseDouble(TrainPercentText, out var trainPercent) || trainPercent <= 0 || trainPercent >= 100)
            {
                throw new InvalidOperationException("Параметр \"Доля выборки, %\" должен быть в диапазоне (0, 100).");
            }

            if (!TryParseDouble(KText, out var k) || k < 0)
            {
                throw new InvalidOperationException("k должен быть неотрицательным.");
            }

            if (!TryParseDouble(AlphaText, out var alpha) || alpha < 0)
            {
                throw new InvalidOperationException("alpha должен быть неотрицательным.");
            }

            if (!int.TryParse(MaxMemoryMText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxMemoryM) ||
                maxMemoryM < 1)
            {
                throw new InvalidOperationException("MaxMemoryM должен быть целым числом >= 1.");
            }

            if (!int.TryParse(MaxPlotPointsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxPlotPoints) ||
                maxPlotPoints < 100)
            {
                throw new InvalidOperationException("MaxPlotPoints должен быть целым числом >= 100.");
            }

            var loadedData = _loadedData;
            var computed = await Task.Run(
                () => ComputeForecast(loadedData, trainPercent, k, alpha, maxMemoryM, maxPlotPoints, _shutdownCts.Token),
                _shutdownCts.Token);

            if (_shutdownCts.IsCancellationRequested)
            {
                return;
            }

            _bestResult = computed.BestResult;
            ErrorRows.Clear();
            foreach (var row in computed.ErrorRows)
            {
                ErrorRows.Add(row);
            }

            BestRows.Clear();
            BestRows.Add(computed.BestRow);
            _bestDates = computed.BestDates;
            PlotModel = computed.PlotModel;

            StatusMessage =
                $"Расчет обновлен. Лучшее m = {computed.BestResult.Memory}, MAE = {FormatPercent(computed.BestMaePercent)}, MSE = {FormatPercent(computed.BestMsePercent)}, RMSE = {FormatPercent(computed.BestRmsePercent)}, MAPE = {computed.BestResult.MapePercent:F2}%.";
            ExportResultsCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Операция отменена.";
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                StatusMessage = $"Ошибка расчета: {ex.Message}";
            }
        }
        finally
        {
            if (manageBusy)
            {
                SetBusy(false);
            }
        }
    }

    private void AutoRefresh()
    {
        if (_isInternalUpdate || _sourceData is null || _isBusy)
        {
            return;
        }

        _ = RunAsync(showErrors: false);
    }

    private bool TryApplyDateFilter(bool showErrors)
    {
        if (_sourceData is null)
        {
            return false;
        }

        var sourcePrices = _sourceData.ClosePrices;
        var sourceDates = _sourceData.Dates;
        if (sourcePrices.Count != sourceDates.Count)
        {
            if (showErrors)
            {
                StatusMessage = "Ошибка данных: количество дат не совпадает с количеством цен.";
            }

            return false;
        }

        if (!TryParseDate(FromDateText, out var fromDate) || !TryParseDate(TillDateText, out var tillDate))
        {
            if (showErrors)
            {
                StatusMessage = "Укажите диапазон дат в формате yyyy-MM-dd.";
            }

            return false;
        }

        if (tillDate < fromDate)
        {
            if (showErrors)
            {
                StatusMessage = "Дата \"По\" должна быть не раньше даты \"С\".";
            }

            return false;
        }

        var filteredPrices = new List<double>(sourcePrices.Count);
        var filteredDates = new List<DateTime?>(sourceDates.Count);
        var hasDateValues = sourceDates.Any(d => d.HasValue);

        if (hasDateValues)
        {
            for (var i = 0; i < sourcePrices.Count; i++)
            {
                var date = sourceDates[i];
                if (!date.HasValue)
                {
                    continue;
                }

                if (date.Value.Date < fromDate.Date || date.Value.Date > tillDate.Date)
                {
                    continue;
                }

                filteredPrices.Add(sourcePrices[i]);
                filteredDates.Add(date.Value.Date);
            }
        }
        else
        {
            filteredPrices.AddRange(sourcePrices);
            filteredDates.AddRange(sourceDates);
        }

        if (filteredPrices.Count < 2)
        {
            ResetCalculatedState();
            if (showErrors)
            {
                StatusMessage = "После фильтрации по датам осталось меньше двух цен.";
            }

            return false;
        }

        _loadedData = new CsvImportedData
        {
            ClosePrices = filteredPrices,
            Dates = filteredDates
        };

        RunCommand.RaiseCanExecuteChanged();
        return true;
    }

    private RunComputationResult ComputeForecast(
        CsvImportedData loadedData,
        double trainPercent,
        double k,
        double alpha,
        int maxMemoryM,
        int maxPlotPoints,
        CancellationToken cancellationToken)
    {
        var prices = loadedData.ClosePrices;
        var returns = _returnCalculator.CalculateLogReturns(prices);
        var encodedStates = _encoder.Encode(returns, k);

        var trainReturnsCount = (int)Math.Round(returns.Count * (trainPercent / 100.0), MidpointRounding.AwayFromZero);
        trainReturnsCount = Math.Max(2, Math.Min(trainReturnsCount, returns.Count - 1));

        var errorRows = new List<MemoryErrorRow>();
        ForecastResult? bestResult = null;
        for (var m = 1; m <= maxMemoryM; m++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m >= trainReturnsCount)
            {
                break;
            }

            var result = _forecaster.Forecast(
                prices,
                returns,
                encodedStates,
                trainReturnsCount,
                m,
                alpha,
                _trainer,
                _metricsService);

            errorRows.Add(new MemoryErrorRow
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
            throw new InvalidOperationException("Не удалось построить модель. Проверьте параметры \"Доля выборки, %\" и \"max(m)\".");
        }

        var bestMaePercent = CalculateRelativePercent(bestResult.Mae, bestResult.ActualPrices);
        var bestMsePercent = CalculateRelativeSquaredPercent(bestResult.Mse, bestResult.ActualPrices);
        var bestRmsePercent = CalculateRelativePercent(bestResult.Rmse, bestResult.ActualPrices);
        var bestRow = new BestMemoryErrorRow
        {
            Memory = bestResult.Memory,
            MaePercent = bestMaePercent,
            MsePercent = bestMsePercent,
            RmsePercent = bestRmsePercent,
            MapePercent = bestResult.MapePercent
        };

        var bestDates = BuildBestDates(loadedData.Dates, bestResult.TrainReturnsCount, bestResult.ActualPrices.Count);
        var plotModel = BuildPlot(bestResult, loadedData.ClosePrices, loadedData.Dates, maxPlotPoints);

        return new RunComputationResult
        {
            BestResult = bestResult,
            ErrorRows = errorRows,
            BestRow = bestRow,
            BestDates = bestDates,
            PlotModel = plotModel,
            BestMaePercent = bestMaePercent,
            BestMsePercent = bestMsePercent,
            BestRmsePercent = bestRmsePercent
        };
    }

    private void ResetCalculatedState()
    {
        _bestResult = null;
        _bestDates = new List<DateTime?>();
        ErrorRows.Clear();
        BestRows.Clear();
        PlotModel = BuildEmptyPlotModel();
        ExportResultsCommand.RaiseCanExecuteChanged();
    }

    private void InitializeDateRangeFromData(CsvImportedData data)
    {
        var dateBounds = data.Dates
            .Where(d => d.HasValue)
            .Select(d => d!.Value.Date)
            .OrderBy(d => d)
            .ToArray();

        if (dateBounds.Length == 0)
        {
            return;
        }

        _isInternalUpdate = true;
        try
        {
            FromDateText = dateBounds.First().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            TillDateText = dateBounds.Last().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private void ExportResults()
    {
        if (_bestResult is null)
        {
            StatusMessage = "Нет результатов для экспорта.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV-файлы (*.csv)|*.csv",
            FileName = "результаты_прогноза.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var metricRows = ErrorRows.Select(row => new MemoryMetricCsvRow
            {
                Memory = row.Memory,
                Mae = row.Mae,
                Mse = row.Mse,
                Rmse = row.Rmse,
                Mape = row.Mape
            }).ToArray();

            var forecastRows = _bestResult.ActualPrices
                .Select((actualPrice, i) => new ForecastPointCsvRow
                {
                    Date = i < _bestDates.Count ? _bestDates[i] : null,
                    ActualPrice = actualPrice,
                    PredictedPrice = _bestResult.PredictedPrices[i]
                })
                .ToArray();

            _csvExportService.ExportCombined(dialog.FileName, metricRows, forecastRows, _bestResult.Memory);
            StatusMessage = $"Результаты экспортированы: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
    }

    private static bool TryParseDouble(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool TryParseDate(string text, out DateTime value)
    {
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out value);
    }

    private static double CalculateRelativePercent(double metricValue, IReadOnlyList<double> actualPrices)
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

    private static double CalculateRelativeSquaredPercent(double metricValue, IReadOnlyList<double> actualPrices)
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

    private static string FormatPercent(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) ? "н/д" : $"{value:F2}%";
    }

    private void SetBusy(bool value)
    {
        if (IsBusy == value)
        {
            return;
        }

        IsBusy = value;
        LoadCsvCommand.RaiseCanExecuteChanged();
        LoadMoexCommand.RaiseCanExecuteChanged();
        RunCommand.RaiseCanExecuteChanged();
        ExportResultsCommand.RaiseCanExecuteChanged();
    }

    private static List<DateTime?> BuildBestDates(IReadOnlyList<DateTime?> allDates, int trainReturnsCount, int forecastCount)
    {
        var result = new List<DateTime?>(forecastCount);
        for (var i = 0; i < forecastCount; i++)
        {
            var dateIndex = trainReturnsCount + 1 + i;
            result.Add(dateIndex >= 0 && dateIndex < allDates.Count ? allDates[dateIndex] : null);
        }

        return result;
    }

    private static PlotModel BuildEmptyPlotModel()
    {
        var model = new PlotModel { Title = "Прогноз цены" };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Дата" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Цена" });
        return model;
    }

    private static PlotModel BuildPlot(
        ForecastResult bestResult,
        IReadOnlyList<double> allPrices,
        IReadOnlyList<DateTime?> allCsvDates,
        int maxPlotPoints)
    {
        var model = new PlotModel
        {
            Title = $"Прогноз цены (лучшее m = {bestResult.Memory})",
            IsLegendVisible = true
        };
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightTop,
            LegendPlacement = LegendPlacement.Outside
        });

        var csvDateBounds = allCsvDates
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderBy(d => d)
            .ToArray();

        var hasCsvDates = csvDateBounds.Length > 0;
        var useDateAxis = hasCsvDates;

        var yValues = allPrices
            .Concat(bestResult.PredictedPrices)
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToArray();

        if (yValues.Length == 0)
        {
            return BuildEmptyPlotModel();
        }

        var minY = yValues.Min();
        var maxY = yValues.Max();
        if (Math.Abs(maxY - minY) < 1e-12)
        {
            var delta = Math.Abs(minY) > 1e-12 ? Math.Abs(minY) * 0.01 : 1.0;
            minY -= delta;
            maxY += delta;
        }

        if (useDateAxis)
        {
            var minDate = csvDateBounds.First();
            var maxDate = csvDateBounds.Last();
            if (maxDate <= minDate)
            {
                maxDate = minDate.AddDays(1);
            }

            var totalDays = Math.Max(1.0, (maxDate - minDate).TotalDays);
            var majorStepDays = Math.Max(1.0, Math.Ceiling(totalDays / 8.0));

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Дата",
                StringFormat = "yyyy-MM-dd",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                MajorStep = majorStepDays,
                MinorStep = Math.Max(1.0, Math.Floor(majorStepDays / 2.0)),
                Angle = 45,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = DateTimeAxis.ToDouble(minDate),
                Maximum = DateTimeAxis.ToDouble(maxDate),
                AbsoluteMinimum = DateTimeAxis.ToDouble(minDate),
                AbsoluteMaximum = DateTimeAxis.ToDouble(maxDate),
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
        }
        else
        {
            var maxX = Math.Max(1, bestResult.ActualPrices.Count - 1);
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Дата",
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = 0,
                Maximum = maxX,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = maxX,
                IsPanEnabled = false,
                IsZoomEnabled = false
            });
        }

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Цена",
            MinimumPadding = 0,
            MaximumPadding = 0,
            Minimum = minY,
            Maximum = maxY,
            AbsoluteMinimum = minY,
            AbsoluteMaximum = maxY,
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        var actualSeries = new LineSeries { Title = "Изначальная цена", StrokeThickness = 2 };
        var predictedSeries = new LineSeries { Title = "Прогнозная цена", StrokeThickness = 2 };
        var actualPlotIndices = BuildPlotIndices(allPrices.Count, maxPlotPoints);
        var forecastStartIndex = bestResult.TrainReturnsCount + 1;
        var predictedPlotIndices = BuildPlotIndices(bestResult.PredictedPrices.Count, maxPlotPoints);

        foreach (var i in actualPlotIndices)
        {
            if (i < 0 || i >= allPrices.Count)
            {
                continue;
            }

            if (double.IsNaN(allPrices[i]) || double.IsInfinity(allPrices[i]))
            {
                continue;
            }

            var x = (double)i;
            if (useDateAxis)
            {
                DateTime pointDate;
                if (i < allCsvDates.Count && allCsvDates[i].HasValue)
                {
                    pointDate = allCsvDates[i]!.Value;
                }
                else
                {
                    var minDate = csvDateBounds.First();
                    var maxDate = csvDateBounds.Last();
                    var totalDays = Math.Max(1.0, (maxDate - minDate).TotalDays);
                    var ratio = allPrices.Count > 1
                        ? (double)i / (allPrices.Count - 1)
                        : 0.0;
                    pointDate = minDate.AddDays(totalDays * ratio);
                }

                x = DateTimeAxis.ToDouble(pointDate);
            }

            actualSeries.Points.Add(new DataPoint(x, allPrices[i]));
        }

        foreach (var localIndex in predictedPlotIndices)
        {
            if (localIndex < 0 || localIndex >= bestResult.PredictedPrices.Count)
            {
                continue;
            }

            var predictedPrice = bestResult.PredictedPrices[localIndex];
            if (double.IsNaN(predictedPrice) || double.IsInfinity(predictedPrice))
            {
                continue;
            }

            var globalIndex = forecastStartIndex + localIndex;
            var x = (double)globalIndex;
            if (useDateAxis)
            {
                DateTime pointDate;
                if (globalIndex < allCsvDates.Count && allCsvDates[globalIndex].HasValue)
                {
                    pointDate = allCsvDates[globalIndex]!.Value;
                }
                else
                {
                    var minDate = csvDateBounds.First();
                    var maxDate = csvDateBounds.Last();
                    var totalDays = Math.Max(1.0, (maxDate - minDate).TotalDays);
                    var ratio = allPrices.Count > 1
                        ? (double)Math.Min(globalIndex, allPrices.Count - 1) / (allPrices.Count - 1)
                        : 0.0;
                    pointDate = minDate.AddDays(totalDays * ratio);
                }

                x = DateTimeAxis.ToDouble(pointDate);
            }

            predictedSeries.Points.Add(new DataPoint(x, predictedPrice));
        }

        model.Series.Add(actualSeries);
        model.Series.Add(predictedSeries);
        return model;
    }

    private static IPlotController BuildLockedPlotController()
    {
        var controller = new PlotController();
        controller.UnbindAll();
        return controller;
    }

    private static IReadOnlyList<int> BuildPlotIndices(int totalCount, int maxPoints)
    {
        if (totalCount <= 0)
        {
            return Array.Empty<int>();
        }

        if (totalCount <= maxPoints || maxPoints < 2)
        {
            return Enumerable.Range(0, totalCount).ToArray();
        }

        var result = new List<int>(maxPoints);
        var step = (double)(totalCount - 1) / (maxPoints - 1);
        var previous = -1;

        for (var i = 0; i < maxPoints; i++)
        {
            var index = (int)Math.Round(i * step, MidpointRounding.AwayFromZero);
            if (index >= totalCount)
            {
                index = totalCount - 1;
            }

            if (index == previous)
            {
                continue;
            }

            result.Add(index);
            previous = index;
        }

        if (result[^1] != totalCount - 1)
        {
            result.Add(totalCount - 1);
        }

        return result;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

