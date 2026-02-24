using System.Collections.Generic;

namespace CaForecast.Core;

public class ForecastResult
{
    public int Memory { get; init; }

    public int TrainReturnsCount { get; init; }

    public IReadOnlyList<double> ActualReturns { get; init; } = new List<double>();

    public IReadOnlyList<double> PredictedReturns { get; init; } = new List<double>();

    public IReadOnlyList<double> ActualPrices { get; init; } = new List<double>();

    public IReadOnlyList<double> PredictedPrices { get; init; } = new List<double>();

    public double Mae { get; init; }

    public double Mse { get; init; }

    public double Rmse { get; init; }

    public double MapePercent { get; init; }
}
