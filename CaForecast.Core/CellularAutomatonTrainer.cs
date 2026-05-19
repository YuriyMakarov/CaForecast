using System.Globalization;

namespace CaForecast.Core;

public sealed class CellularAutomatonTrainer
{
    private static readonly int[] SupportedStates = [-1, 0, 1];

    public CellularAutomatonModel Train(
        IReadOnlyList<int> states,
        IReadOnlyList<double> returns,
        int memoryDepthM,
        double smoothingAlpha)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(returns);

        if (states.Count != returns.Count)
        {
            throw new ArgumentException("Количество состояний должно совпадать с количеством доходностей.");
        }

        if (memoryDepthM < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(memoryDepthM), "Глубина памяти должна быть не меньше 1.");
        }

        if (states.Count <= memoryDepthM)
        {
            throw new ArgumentException("Обучающая выборка недостаточна для заданной глубины памяти.", nameof(states));
        }

        if (smoothingAlpha < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothingAlpha), "Параметр alpha не может быть отрицательным.");
        }

        var countsByPattern = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var globalCounts = new double[3];

        for (var index = memoryDepthM; index < states.Count; index++)
        {
            var pattern = BuildPattern(states, index - memoryDepthM, memoryDepthM);
            var stateIndex = ToProbabilityIndex(states[index]);

            if (!countsByPattern.TryGetValue(pattern, out var counts))
            {
                counts = new double[3];
                countsByPattern[pattern] = counts;
            }

            counts[stateIndex]++;
            globalCounts[stateIndex]++;
        }

        var probabilities = new Dictionary<string, double[]>(countsByPattern.Count, StringComparer.Ordinal);
        foreach (var pair in countsByPattern)
        {
            probabilities[pair.Key] = ApplyLaplace(pair.Value, smoothingAlpha);
        }

        return new CellularAutomatonModel
        {
            MemoryDepthM = memoryDepthM,
            TransitionProbabilities = probabilities,
            MeanReturnsByState = BuildMeanReturnsByState(states, returns),
            GlobalDistribution = ApplyLaplace(globalCounts, smoothingAlpha)
        };
    }

    public static string BuildPattern(IReadOnlyList<int> states, int startIndex, int memoryDepthM)
    {
        var buffer = new string[memoryDepthM];
        for (var offset = 0; offset < memoryDepthM; offset++)
        {
            buffer[offset] = states[startIndex + offset].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join('|', buffer);
    }

    private static Dictionary<int, double> BuildMeanReturnsByState(IReadOnlyList<int> states, IReadOnlyList<double> returns)
    {
        var sums = new Dictionary<int, double> { [-1] = 0.0, [0] = 0.0, [1] = 0.0 };
        var counts = new Dictionary<int, int> { [-1] = 0, [0] = 0, [1] = 0 };

        for (var index = 0; index < states.Count; index++)
        {
            sums[states[index]] += returns[index];
            counts[states[index]]++;
        }

        var meanReturns = new Dictionary<int, double>(3);
        foreach (var state in SupportedStates)
        {
            meanReturns[state] = counts[state] == 0 ? 0.0 : sums[state] / counts[state];
        }

        return meanReturns;
    }

    private static int ToProbabilityIndex(int state)
    {
        return state switch
        {
            -1 => 0,
            0 => 1,
            1 => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(state), "Допустимые состояния: -1, 0, 1.")
        };
    }

    private static double[] ApplyLaplace(IReadOnlyList<double> counts, double smoothingAlpha)
    {
        var denominator = counts.Sum() + (3.0 * smoothingAlpha);
        if (denominator <= 0)
        {
            return [1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0];
        }

        return
        [
            (counts[0] + smoothingAlpha) / denominator,
            (counts[1] + smoothingAlpha) / denominator,
            (counts[2] + smoothingAlpha) / denominator
        ];
    }
}
