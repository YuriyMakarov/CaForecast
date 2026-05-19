namespace CaForecast.WpfApp.ViewModels;

public sealed class MemoryCandidateRowViewModel
{
    public int MemoryDepthM { get; init; }

    public double Mae { get; init; }

    public double Mse { get; init; }

    public double Rmse { get; init; }

    public double Mape { get; init; }
}
