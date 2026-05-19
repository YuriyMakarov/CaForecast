namespace CaForecast.WpfApp.ViewModels;

public sealed class ForecastPointRowViewModel
{
    public string Period { get; init; } = string.Empty;

    public double ActualValue { get; init; }

    public double PredictedValue { get; init; }
}
