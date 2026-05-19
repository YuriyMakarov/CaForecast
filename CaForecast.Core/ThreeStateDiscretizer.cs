namespace CaForecast.Core;

public sealed class ThreeStateDiscretizer
{
    public IReadOnlyList<int> Discretize(IReadOnlyList<double> returns, double thresholdK)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (thresholdK < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdK), "Порог k не может быть отрицательным.");
        }

        var states = new int[returns.Count];
        for (var index = 0; index < returns.Count; index++)
        {
            states[index] = DiscretizeSingle(returns[index], thresholdK);
        }

        return states;
    }

    public int DiscretizeSingle(double value, double thresholdK)
    {
        if (value > thresholdK)
        {
            return 1;
        }

        if (value < -thresholdK)
        {
            return -1;
        }

        return 0;
    }
}
