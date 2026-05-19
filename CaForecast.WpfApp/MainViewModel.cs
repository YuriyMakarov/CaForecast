using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CaForecast.Core;
using CaForecast.Data.Models;
using CaForecast.Data.Services;
using CaForecast.WpfApp.ViewModels;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CaForecast.WpfApp;

public sealed class MainViewModel : ViewModelBase
{
    private readonly HistoricalMetricCsvImportService _csvImportService;
    private readonly DirectionQueryService _directionQueryService;
    private readonly PredictionResultService _predictionResultService;
    private readonly StoredSeriesManagementService _storedSeriesManagementService;
    private readonly SalesForecastService _forecastService = new();

    private readonly SemaphoreSlim _calculationGate = new(1, 1);
    private CancellationTokenSource? _recalculationCts;
    private bool _suppressAutoRefresh;
    private bool _isBusy;
    private string _selectedFilePath = "Файл не выбран";
    private string _statusMessage = "Загрузите CSV с историей продаж, лидов или договоров.";
    private DirectionItemViewModel? _selectedDirection;
    private string _directionNameOverride = string.Empty;
    private int _memoryDepthM = 4;
    private double _thresholdK = 0.015;
    private double _smoothingAlpha = 1.0;
    private double _trainRatioPercent = 80;
    private int _maxMemoryDepthForSearch = 12;
    private ForecastScenarioResult? _lastScenario;
    private PlotModel _plotModel = BuildEmptyPlotModel("Полный ряд");
    private PlotModel _forecastFocusPlotModel = BuildEmptyPlotModel("Фокус на прогнозе");
    private string _selectedPlotMode = "Полный ряд";
    private string _maeText = "-";
    private string _mseText = "-";
    private string _rmseText = "-";
    private string _mapeText = "-";
    private string _fallbackText = "-";
    private DateTime? _periodFrom;
    private DateTime? _periodTo;
    private DateTime? _availablePeriodFrom;
    private DateTime? _availablePeriodTo;

    public MainViewModel(
        HistoricalMetricCsvImportService csvImportService,
        DirectionQueryService directionQueryService,
        PredictionResultService predictionResultService,
        StoredSeriesManagementService storedSeriesManagementService)
    {
        _csvImportService = csvImportService;
        _directionQueryService = directionQueryService;
        _predictionResultService = predictionResultService;
        _storedSeriesManagementService = storedSeriesManagementService;

        ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync, () => !IsBusy);
        FindBestMemoryCommand = new AsyncRelayCommand(FindBestMemoryAsync, () => !IsBusy && SelectedDirection is not null);
        DeleteSelectedDirectionCommand = new AsyncRelayCommand(DeleteSelectedDirectionAsync, () => !IsBusy && SelectedDirection is not null);
        DeleteAllDirectionsCommand = new AsyncRelayCommand(DeleteAllDirectionsAsync, () => !IsBusy && Directions.Count > 0);
        ExportForecastCommand = new RelayCommand(ExportForecast, () => _lastScenario is not null && !IsBusy);
    }

    public AsyncRelayCommand ImportCsvCommand { get; }

    public AsyncRelayCommand FindBestMemoryCommand { get; }

    public AsyncRelayCommand DeleteSelectedDirectionCommand { get; }

    public AsyncRelayCommand DeleteAllDirectionsCommand { get; }

    public RelayCommand ExportForecastCommand { get; }

    public ObservableCollection<DirectionItemViewModel> Directions { get; } = [];

    public ObservableCollection<MemoryCandidateRowViewModel> MemoryCandidates { get; } = [];

    public ObservableCollection<ForecastPointRowViewModel> ForecastRows { get; } = [];

    public IReadOnlyList<string> PlotModes { get; } = ["Полный ряд", "Фокус на прогнозе"];

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set => SetProperty(ref _selectedFilePath, value);
    }

    public string DirectionNameOverride
    {
        get => _directionNameOverride;
        set => SetProperty(ref _directionNameOverride, value);
    }

    public DirectionItemViewModel? SelectedDirection
    {
        get => _selectedDirection;
        set
        {
            if (SetProperty(ref _selectedDirection, value))
            {
                RaiseCommandStates();
                _ = LoadSelectedDirectionRangeAsync();
            }
        }
    }

    public int MemoryDepthM
    {
        get => _memoryDepthM;
        set
        {
            if (SetProperty(ref _memoryDepthM, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public double ThresholdK
    {
        get => _thresholdK;
        set
        {
            if (SetProperty(ref _thresholdK, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public double SmoothingAlpha
    {
        get => _smoothingAlpha;
        set
        {
            if (SetProperty(ref _smoothingAlpha, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public double TrainRatioPercent
    {
        get => _trainRatioPercent;
        set
        {
            if (SetProperty(ref _trainRatioPercent, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public int MaxMemoryDepthForSearch
    {
        get => _maxMemoryDepthForSearch;
        set => SetProperty(ref _maxMemoryDepthForSearch, value);
    }

    public DateTime? AvailablePeriodFrom
    {
        get => _availablePeriodFrom;
        private set => SetProperty(ref _availablePeriodFrom, value);
    }

    public DateTime? AvailablePeriodTo
    {
        get => _availablePeriodTo;
        private set => SetProperty(ref _availablePeriodTo, value);
    }

    public DateTime? PeriodFrom
    {
        get => _periodFrom;
        set
        {
            if (SetProperty(ref _periodFrom, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public DateTime? PeriodTo
    {
        get => _periodTo;
        set
        {
            if (SetProperty(ref _periodTo, value))
            {
                ScheduleRecalculation();
            }
        }
    }

    public string SelectedPlotMode
    {
        get => _selectedPlotMode;
        set
        {
            if (SetProperty(ref _selectedPlotMode, value))
            {
                RaisePropertyChanged(nameof(CurrentPlotModel));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public PlotModel PlotModel
    {
        get => _plotModel;
        private set
        {
            if (SetProperty(ref _plotModel, value))
            {
                RaisePropertyChanged(nameof(CurrentPlotModel));
            }
        }
    }

    public PlotModel ForecastFocusPlotModel
    {
        get => _forecastFocusPlotModel;
        private set
        {
            if (SetProperty(ref _forecastFocusPlotModel, value))
            {
                RaisePropertyChanged(nameof(CurrentPlotModel));
            }
        }
    }

    public PlotModel CurrentPlotModel => SelectedPlotMode == "Фокус на прогнозе"
        ? ForecastFocusPlotModel
        : PlotModel;

    public string MaeText
    {
        get => _maeText;
        private set => SetProperty(ref _maeText, value);
    }

    public string MseText
    {
        get => _mseText;
        private set => SetProperty(ref _mseText, value);
    }

    public string RmseText
    {
        get => _rmseText;
        private set => SetProperty(ref _rmseText, value);
    }

    public string MapeText
    {
        get => _mapeText;
        private set => SetProperty(ref _mapeText, value);
    }

    public string FallbackText
    {
        get => _fallbackText;
        private set => SetProperty(ref _fallbackText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadDirectionsAsync(cancellationToken, selectFirstIfMissing: true);
        if (Directions.Count == 0)
        {
            ResetViewState();
            return;
        }

        StatusMessage = $"Загружено сохраненных наборов: {Directions.Count}.";
    }

    public void CancelPendingWork()
    {
        _recalculationCts?.Cancel();
    }

    private async Task ImportCsvAsync()
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
            IsBusy = true;
            StatusMessage = "Импорт CSV в SQLite...";

            var importResult = await _csvImportService.ImportAsync(
                dialog.FileName,
                new CsvImportOptions
                {
                    DirectionNameOverride = string.IsNullOrWhiteSpace(DirectionNameOverride) ? null : DirectionNameOverride,
                    ReplaceExistingSeries = false
                });

            SelectedFilePath = dialog.FileName;
            await LoadDirectionsAsync(CancellationToken.None, selectFirstIfMissing: false);
            SelectedDirection = Directions.FirstOrDefault(x => x.Id == importResult.DirectionId);
            StatusMessage = $"Импорт завершен: {importResult.DirectionName}. Загружено {importResult.ImportedRows}, пропущено {importResult.SkippedRows}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка импорта CSV: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDirectionsAsync(CancellationToken cancellationToken, bool selectFirstIfMissing)
    {
        var directions = await _directionQueryService.GetDirectionsAsync(cancellationToken);

        _suppressAutoRefresh = true;
        try
        {
            Directions.Clear();
            foreach (var direction in directions)
            {
                Directions.Add(new DirectionItemViewModel
                {
                    Id = direction.Id,
                    Name = direction.Name
                });
            }

            if (SelectedDirection is null && Directions.Count > 0 && selectFirstIfMissing)
            {
                SelectedDirection = Directions[0];
            }
            else if (SelectedDirection is not null)
            {
                SelectedDirection = Directions.FirstOrDefault(x => x.Id == SelectedDirection.Id);

                if (SelectedDirection is null)
                {
                    ResetViewState();
                }
            }
        }
        finally
        {
            _suppressAutoRefresh = false;
            RaiseCommandStates();
        }
    }

    private async Task DeleteSelectedDirectionAsync()
    {
        if (SelectedDirection is null)
        {
            return;
        }

        var direction = SelectedDirection;
        var confirmation = MessageBox.Show(
            $"Удалить сохраненный набор «{direction.Name}» вместе с историей и сохраненными результатами прогноза?",
            "Удаление набора",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Удаление набора «{direction.Name}»...";

            CancelPendingWork();
            await _storedSeriesManagementService.DeleteDirectionAsync(direction.Id, CancellationToken.None);

            ResetViewState();
            await LoadDirectionsAsync(CancellationToken.None, selectFirstIfMissing: true);

            StatusMessage = Directions.Count == 0
                ? "Сохраненный набор удален. База сохраненных рядов пуста."
                : $"Сохраненный набор «{direction.Name}» удален. Осталось наборов: {Directions.Count}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка удаления набора: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAllDirectionsAsync()
    {
        if (Directions.Count == 0)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            "Удалить все сохраненные наборы, историю метрик и сохраненные результаты прогнозов?",
            "Полная очистка",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Удаление всех сохраненных наборов...";

            CancelPendingWork();
            await _storedSeriesManagementService.DeleteAllDirectionsAsync(CancellationToken.None);

            ResetViewState();
            await LoadDirectionsAsync(CancellationToken.None, selectFirstIfMissing: false);
            StatusMessage = "Все сохраненные наборы удалены.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка полной очистки: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectedDirectionRangeAsync()
    {
        if (SelectedDirection is null)
        {
            ResetViewState();
            return;
        }

        try
        {
            _suppressAutoRefresh = true;
            var series = await _directionQueryService.GetSeriesAsync(SelectedDirection.Id, CancellationToken.None);
            if (series.Count == 0)
            {
                AvailablePeriodFrom = null;
                AvailablePeriodTo = null;
                PeriodFrom = null;
                PeriodTo = null;
                return;
            }

            var minDate = series[0].Date.ToDateTime(TimeOnly.MinValue);
            var maxDate = series[^1].Date.ToDateTime(TimeOnly.MinValue);

            AvailablePeriodFrom = minDate;
            AvailablePeriodTo = maxDate;

            if (PeriodFrom is null || PeriodFrom < minDate || PeriodFrom > maxDate)
            {
                PeriodFrom = minDate;
            }

            if (PeriodTo is null || PeriodTo < minDate || PeriodTo > maxDate)
            {
                PeriodTo = maxDate;
            }

            if (PeriodFrom > PeriodTo)
            {
                PeriodFrom = minDate;
                PeriodTo = maxDate;
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка чтения диапазона дат: {exception.Message}";
        }
        finally
        {
            _suppressAutoRefresh = false;
        }

        ScheduleRecalculation();
    }

    private void ScheduleRecalculation()
    {
        if (_suppressAutoRefresh || SelectedDirection is null)
        {
            return;
        }

        _recalculationCts?.Cancel();
        _recalculationCts?.Dispose();
        _recalculationCts = new CancellationTokenSource();
        var token = _recalculationCts.Token;

        _ = ScheduleRecalculationAsync(token);
    }

    private async Task ScheduleRecalculationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await RecalculateAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RecalculateAsync(CancellationToken cancellationToken)
    {
        if (SelectedDirection is null)
        {
            return;
        }

        await _calculationGate.WaitAsync(cancellationToken);
        try
        {
            IsBusy = true;
            StatusMessage = "Пересчет модели...";

            var series = await _directionQueryService.GetSeriesAsync(SelectedDirection.Id, cancellationToken);
            var filteredSeries = series;
            if (filteredSeries.Count < 6)
            {
                throw new InvalidOperationException("На выбранном временном промежутке недостаточно исторических данных.");
            }

            var maxAllowedMemoryDepth = GetMaxAllowedMemoryDepth(filteredSeries.Count);
            if (MemoryDepthM > maxAllowedMemoryDepth)
            {
                StatusMessage = $"Для выбранного периода глубина памяти m должна быть не больше {maxAllowedMemoryDepth}. Уменьшите m или расширьте диапазон дат.";
                return;
            }

            var values = filteredSeries.Select(x => x.Value).ToArray();
            var periods = filteredSeries.Select(x => (DateOnly?)x.Date).ToArray();
            var parameters = new ForecastingParameters
            {
                MemoryDepthM = MemoryDepthM,
                ThresholdK = ThresholdK,
                SmoothingAlpha = SmoothingAlpha,
                TrainRatio = TrainRatioPercent / 100.0
            };

            var scenario = await Task.Run(() => _forecastService.BuildForecast(values, periods, parameters, cancellationToken), cancellationToken);
            _lastScenario = scenario;

            UpdateMetrics(scenario);
            UpdateForecastRows(scenario);
            PlotModel = BuildFullPlotModel(SelectedDirection.Name, periods, values, scenario);
            ForecastFocusPlotModel = BuildForecastFocusPlotModel(SelectedDirection.Name, periods, values, scenario);

            await _predictionResultService.SaveAsync(
                new ExperimentLogEntry
                {
                    DirectionId = SelectedDirection.Id,
                    MemoryDepthM = scenario.Parameters.MemoryDepthM,
                    ThresholdK = scenario.Parameters.ThresholdK,
                    SmoothingAlpha = scenario.Parameters.SmoothingAlpha,
                    Mae = scenario.Metrics.Mae,
                    Rmse = scenario.Metrics.Rmse,
                    Mape = scenario.Metrics.Mape,
                    PredictedValuesJson = JsonSerializer.Serialize(scenario.PredictedValues)
                },
                cancellationToken);

            StatusMessage = $"Расчет завершен для направления «{SelectedDirection.Name}» на периоде {periods[0]:yyyy-MM-dd} - {periods[^1]:yyyy-MM-dd}.";
            var completionMessage = $"Расчет завершен для направления «{SelectedDirection.Name}» на периоде {periods[0]:yyyy-MM-dd} - {periods[^1]:yyyy-MM-dd}.";
            StatusMessage = scenario.ForecastPoints.Count <= 1
                ? $"{completionMessage} Предупреждение: прогнозная часть содержит только 1 точку, поэтому линия прогноза может быть почти не видна. Расширьте диапазон дат или уменьшите обучающую выборку."
                : completionMessage;
            ExportForecastCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка расчета: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            _calculationGate.Release();
        }
    }

    private async Task FindBestMemoryAsync()
    {
        if (SelectedDirection is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Подбор оптимальной глубины памяти...";

            var series = await _directionQueryService.GetSeriesAsync(SelectedDirection.Id, CancellationToken.None);
            var filteredSeries = series;
            if (filteredSeries.Count < 6)
            {
                throw new InvalidOperationException("На выбранном временном промежутке недостаточно наблюдений.");
            }

            var values = filteredSeries.Select(x => x.Value).ToArray();
            var periods = filteredSeries.Select(x => (DateOnly?)x.Date).ToArray();
            var maxAllowedMemoryDepth = GetMaxAllowedMemoryDepth(filteredSeries.Count);
            var maxMemoryDepthForSearch = Math.Min(MaxMemoryDepthForSearch, maxAllowedMemoryDepth);
            var optimization = await Task.Run(
                () => _forecastService.FindBestMemoryDepth(
                    values,
                    periods,
                    ThresholdK,
                    SmoothingAlpha,
                    TrainRatioPercent / 100.0,
                    1,
                    maxMemoryDepthForSearch,
                    CancellationToken.None));

            MemoryCandidates.Clear();
            foreach (var candidate in optimization.Candidates)
            {
                MemoryCandidates.Add(new MemoryCandidateRowViewModel
                {
                    MemoryDepthM = candidate.MemoryDepthM,
                    Mae = candidate.Metrics.Mae,
                    Mse = candidate.Metrics.Mse,
                    Rmse = candidate.Metrics.Rmse,
                    Mape = candidate.Metrics.Mape
                });
            }

            MemoryDepthM = optimization.BestMemoryDepthM;
            StatusMessage = $"Подбор завершен. Лучшее m = {optimization.BestMemoryDepthM}, RMSE = {optimization.BestScenario.Metrics.Rmse:F4}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка подбора m: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ExportForecast()
    {
        if (_lastScenario is null || SelectedDirection is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV-файлы (*.csv)|*.csv",
            FileName = $"forecast_{SanitizeFileName(SelectedDirection.Name)}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("direction;memory_depth_m;threshold_k;smoothing_alpha;mae;mse;rmse;mape;fallback_usage_count");
        builder.AppendLine(string.Join(
            ';',
            EscapeCsv(SelectedDirection.Name),
            _lastScenario.Parameters.MemoryDepthM.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Parameters.ThresholdK.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Parameters.SmoothingAlpha.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Metrics.Mae.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Metrics.Mse.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Metrics.Rmse.ToString(CultureInfo.InvariantCulture),
            _lastScenario.Metrics.Mape.ToString(CultureInfo.InvariantCulture),
            _lastScenario.FallbackUsageCount.ToString(CultureInfo.InvariantCulture)));

        builder.AppendLine();
        builder.AppendLine("period;actual_value;predicted_value");
        foreach (var point in _lastScenario.ForecastPoints)
        {
            builder.AppendLine(string.Join(
                ';',
                point.Period?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                point.ActualValue.ToString(CultureInfo.InvariantCulture),
                point.PredictedValue.ToString(CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        StatusMessage = $"Результаты выгружены в {dialog.FileName}.";
    }

    private void UpdateMetrics(ForecastScenarioResult scenario)
    {
        MaeText = scenario.Metrics.Mae.ToString("F4", CultureInfo.InvariantCulture);
        MseText = scenario.Metrics.Mse.ToString("F4", CultureInfo.InvariantCulture);
        RmseText = scenario.Metrics.Rmse.ToString("F4", CultureInfo.InvariantCulture);
        MapeText = $"{scenario.Metrics.Mape:F2}%";
        FallbackText = scenario.FallbackUsageCount.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateForecastRows(ForecastScenarioResult scenario)
    {
        ForecastRows.Clear();
        foreach (var point in scenario.ForecastPoints)
        {
            ForecastRows.Add(new ForecastPointRowViewModel
            {
                Period = point.Period?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-",
                ActualValue = point.ActualValue,
                PredictedValue = point.PredictedValue
            });
        }
    }

    private List<(DateOnly Date, double Value)> ApplyPeriodFilter(IReadOnlyList<(DateOnly Date, double Value)> series)
    {
        var fromDate = PeriodFrom?.Date;
        var toDate = PeriodTo?.Date;

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new InvalidOperationException("Начало периода не может быть позже конца периода.");
        }

        return series
            .Where(item =>
                (!fromDate.HasValue || item.Date.ToDateTime(TimeOnly.MinValue).Date >= fromDate.Value) &&
                (!toDate.HasValue || item.Date.ToDateTime(TimeOnly.MinValue).Date <= toDate.Value))
            .ToList();
    }

    private static int GetMaxAllowedMemoryDepth(int observationCount)
    {
        return Math.Max(1, observationCount - 3);
    }

    private void ResetViewState()
    {
        _suppressAutoRefresh = true;
        try
        {
            AvailablePeriodFrom = null;
            AvailablePeriodTo = null;
            PeriodFrom = null;
            PeriodTo = null;
            _lastScenario = null;
            MemoryCandidates.Clear();
            ForecastRows.Clear();
            PlotModel = BuildEmptyPlotModel("Полный ряд");
            ForecastFocusPlotModel = BuildEmptyPlotModel("Фокус на прогнозе");
            MaeText = "-";
            MseText = "-";
            RmseText = "-";
            MapeText = "-";
            FallbackText = "-";
        }
        finally
        {
            _suppressAutoRefresh = false;
            ExportForecastCommand.RaiseCanExecuteChanged();
        }
    }

    private static PlotModel BuildEmptyPlotModel(string title)
    {
        var model = new PlotModel { Title = title };
        model.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, Title = "Период", IsZoomEnabled = true, IsPanEnabled = true });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Значение", IsZoomEnabled = true, IsPanEnabled = true });
        foreach (var axis in model.Axes)
        {
            axis.IsZoomEnabled = false;
            axis.IsPanEnabled = false;
        }

        return model;
    }

    private static PlotModel BuildFullPlotModel(
        string directionName,
        IReadOnlyList<DateOnly?> periods,
        IReadOnlyList<double> actualSeries,
        ForecastScenarioResult scenario)
    {
        return BuildPlotModel(
            $"Полный ряд: {directionName}",
            periods,
            actualSeries,
            scenario.ForecastPoints.Select(x => (x.Period, x.PredictedValue)).ToList());
    }

    private static PlotModel BuildForecastFocusPlotModel(
        string directionName,
        IReadOnlyList<DateOnly?> periods,
        IReadOnlyList<double> actualSeries,
        ForecastScenarioResult scenario)
    {
        var forecastStartIndex = Math.Max(0, scenario.TrainingObservationCount - 1);
        var actualTailStartIndex = Math.Max(0, forecastStartIndex - 1);

        var focusPeriods = periods.Skip(actualTailStartIndex).ToArray();
        var focusActualSeries = actualSeries.Skip(actualTailStartIndex).ToArray();
        var predictedPoints = scenario.ForecastPoints.Select(x => (x.Period, x.PredictedValue)).ToList();

        var model = BuildPlotModel(
            $"Фокус на прогнозе: {directionName}",
            focusPeriods,
            focusActualSeries,
            predictedPoints);

        var forecastStart = predictedPoints.FirstOrDefault(x => x.Period is not null).Period;
        var forecastEnd = predictedPoints.LastOrDefault(x => x.Period is not null).Period;

        if (forecastStart is not null && forecastEnd is not null)
        {
            var dateAxis = model.Axes.OfType<DateTimeAxis>().FirstOrDefault();
            if (dateAxis is not null)
            {
                var min = ToAxisValue(forecastStart.Value);
                var max = ToAxisValue(forecastEnd.Value);
                if (max <= min)
                {
                    max = min + 1;
                }

                dateAxis.Minimum = min;
                dateAxis.Maximum = max;
            }
        }

        return model;
    }

    private static PlotModel BuildPlotModel(
        string title,
        IReadOnlyList<DateOnly?> periods,
        IReadOnlyList<double> actualSeries,
        IReadOnlyList<(DateOnly? Period, double PredictedValue)> predictedPoints)
    {
        var plotModel = new PlotModel
        {
            Title = title
        };

        plotModel.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Период",
            StringFormat = "dd.MM.yyyy",
            Angle = 35,
            IsZoomEnabled = true,
            IsPanEnabled = true
        });
        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Значение метрики",
            IsZoomEnabled = true,
            IsPanEnabled = true
        });

        foreach (var axis in plotModel.Axes)
        {
            axis.IsZoomEnabled = false;
            axis.IsPanEnabled = false;
        }

        var actualSeriesLine = new LineSeries
        {
            Title = "Фактические значения",
            StrokeThickness = 2
        };

        for (var index = 0; index < actualSeries.Count; index++)
        {
            if (index >= periods.Count || periods[index] is null)
            {
                continue;
            }

            actualSeriesLine.Points.Add(new DataPoint(ToAxisValue(periods[index]!.Value), actualSeries[index]));
        }

        var forecastSeriesLine = new LineSeries
        {
            Title = "Прогнозные значения",
            StrokeThickness = 2,
            Color = OxyColors.IndianRed
        };

        foreach (var point in predictedPoints)
        {
            if (point.Period is null)
            {
                continue;
            }

            forecastSeriesLine.Points.Add(new DataPoint(ToAxisValue(point.Period.Value), point.PredictedValue));
        }

        plotModel.Series.Add(actualSeriesLine);
        plotModel.Series.Add(forecastSeriesLine);
        return plotModel;
    }

    private void RaiseCommandStates()
    {
        ImportCsvCommand.RaiseCanExecuteChanged();
        FindBestMemoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedDirectionCommand.RaiseCanExecuteChanged();
        DeleteAllDirectionsCommand.RaiseCanExecuteChanged();
        ExportForecastCommand.RaiseCanExecuteChanged();
    }

    private static double ToAxisValue(DateOnly date)
    {
        return DateTimeAxis.ToDouble(date.ToDateTime(TimeOnly.MinValue));
    }

    private static string EscapeCsv(string value)
    {
        return value.Contains(';') ? $"\"{value}\"" : value;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }
}
