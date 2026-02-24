# CaForecast

Приложение для прогноза цены на основе трёхцветного клеточного автомата (`-1/0/1`) с загрузкой данных из CSV и MOEX ISS API.

## Назначение

Программа:
- загружает исторические данные цены;
- выполняет расчёт прогноза по модели CA;
- считает метрики ошибки (MAE, MSE, RMSE, MAPE);
- показывает результаты в таблицах и на графике;
- экспортирует результаты в CSV.

## Архитектура (слои)

### 1. `CaForecast.WpfApp` — Presentation (UI)
Отвечает за пользовательский интерфейс, ввод параметров, запуск расчётов и отображение результата.

### 2. `CaForecast.Core` — Domain / Business Logic
Содержит логику модели: подготовка данных, обучение правил автомата, прогноз и вычисление метрик.

### 3. `CaForecast.Data` — Infrastructure / Data Access
Работает с внешними источниками данных: импорт/экспорт CSV и загрузка данных с MOEX.

## Структура решения

```text
CaForecast.sln
CaForecast.Core/
  Models/
    CaRuleModel.cs
    ForecastResult.cs
  Services/
    ReturnCalculator.cs
    ThreeColorEncoder.cs
    CaRuleTrainer.cs
    CaForecaster.cs
    MetricsService.cs

CaForecast.Data/
  Models/
    CsvImportedData.cs
    CsvSettings.cs
    ForecastPointCsvRow.cs
    MemoryMetricCsvRow.cs
  Services/
    CsvImportService.cs
    CsvExportService.cs
    MoexIssService.cs

CaForecast.WpfApp/
  App.xaml
  MainWindow.xaml
  Commands/
    RelayCommand.cs
  ViewModels/
    MainViewModel.cs
    Rows/
      MemoryErrorRow.cs
      BestMemoryErrorRow.cs
      RunComputationResult.cs
```

## Коротко по ключевым файлам

- `MainWindow.xaml` — визуальная часть главного окна.
- `MainViewModel.cs` — связывает UI с сервисами и управляет сценарием работы.
- `RelayCommand.cs` — обработка команд интерфейса (кнопки, действия).
- `MoexIssService.cs` — получение исторических данных через MOEX ISS API.
- `CsvImportService.cs` — чтение исходных данных из CSV.
- `CsvExportService.cs` — сохранение результатов в CSV.
- `CaRuleTrainer.cs` — обучение правил клеточного автомата.
- `CaForecaster.cs` — формирование прогноза.
- `MetricsService.cs` — расчёт метрик точности.

## Используемые библиотеки и технологии

- `.NET 8`
- `WPF` (настольный интерфейс)
- `OxyPlot.Wpf` (построение графиков)
- `System.Text.Json` (обработка JSON)
- `HttpClient` (HTTP-запросы к API)

## Зависимости между проектами

- `CaForecast.WpfApp` -> `CaForecast.Core`
- `CaForecast.WpfApp` -> `CaForecast.Data`
- `CaForecast.Core` — независимый слой бизнес-логики
- `CaForecast.Data` — слой доступа к данным

## Как запустить

```powershell
dotnet restore
dotnet build CaForecast.sln
dotnet run --project .\CaForecast.WpfApp\CaForecast.WpfApp.csproj
```

Требование: Windows (так как используется WPF).
