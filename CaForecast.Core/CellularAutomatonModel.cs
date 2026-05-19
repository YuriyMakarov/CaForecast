namespace CaForecast.Core;

public sealed class CellularAutomatonModel
{
    public required int MemoryDepthM { get; init; }

    public required IReadOnlyDictionary<string, double[]> TransitionProbabilities { get; init; }

    public required IReadOnlyDictionary<int, double> MeanReturnsByState { get; init; }

    public required double[] GlobalDistribution { get; init; }
}
