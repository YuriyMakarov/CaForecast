using CaForecast.Core;
using OxyPlot;

namespace CaForecast.WpfApp;

internal sealed class RunComputationResult
{
    public required ForecastResult BestResult { get; init; }

    public required List<MemoryErrorRow> ErrorRows { get; init; }

    public required BestMemoryErrorRow BestRow { get; init; }

    public required List<DateTime?> BestDates { get; init; }

    public required PlotModel PlotModel { get; init; }

    public required double BestMaePercent { get; init; }

    public required double BestMsePercent { get; init; }

    public required double BestRmsePercent { get; init; }
}
