namespace CaForecast.Core;

public sealed class CaRuleModel
{
    public CaRuleModel(int memory, Dictionary<string, double[]> transitionProbabilities, Dictionary<int, double> meanReturnsByState)
    {
        Memory = memory;
        TransitionProbabilities = transitionProbabilities;
        MeanReturnsByState = meanReturnsByState;
    }

    public int Memory { get; }

    public IReadOnlyDictionary<string, double[]> TransitionProbabilities { get; }

    public IReadOnlyDictionary<int, double> MeanReturnsByState { get; }

    public double[] GlobalDistribution { get; init; } = { 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0 };
}
