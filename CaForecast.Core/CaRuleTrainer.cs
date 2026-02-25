using System;
using System.Collections.Generic;
using System.Linq;

namespace CaForecast.Core;

public class CaRuleTrainer
{
    private static readonly int[] StateValues = { -1, 0, 1 };

    public CaRuleModel Train(IReadOnlyList<int> states, IReadOnlyList<double> returns, int memory, double alpha)
    {
        if (states is null)
        {
            throw new ArgumentNullException(nameof(states));
        }

        if (returns is null)
        {
            throw new ArgumentNullException(nameof(returns));
        }

        if (states.Count != returns.Count)
        {
            throw new ArgumentException("Массивы состояний и доходностей должны иметь одинаковую длину.");
        }

        if (states.Count <= memory)
        {
            throw new ArgumentException("Обучающая выборка слишком короткая для выбранной памяти.");
        }

        if (memory < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(memory), "Память должна быть >= 1.");
        }

        if (alpha < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "alpha должен быть неотрицательным.");
        }

        var countsByPattern = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var globalCounts = new double[3];

        for (var t = memory; t < states.Count; t++)
        {
            var pattern = BuildPattern(states, t - memory, memory);
            var nextStateIndex = ToIndex(states[t]);

            if (!countsByPattern.TryGetValue(pattern, out var counts))
            {
                counts = new double[3];
                countsByPattern[pattern] = counts;
            }

            counts[nextStateIndex]++;
            globalCounts[nextStateIndex]++;
        }

        var transitionProbabilities = new Dictionary<string, double[]>(countsByPattern.Count, StringComparer.Ordinal);
        foreach (var pair in countsByPattern)
        {
            transitionProbabilities[pair.Key] = ApplyLaplace(pair.Value, alpha);
        }

        var globalDistribution = ApplyLaplace(globalCounts, alpha);
        var meanReturnsByState = BuildMeanReturnsByState(states, returns);

        return new CaRuleModel(memory, transitionProbabilities, meanReturnsByState)
        {
            GlobalDistribution = globalDistribution
        };
    }

    private static Dictionary<int, double> BuildMeanReturnsByState(IReadOnlyList<int> states, IReadOnlyList<double> returns)
    {
        var sums = new Dictionary<int, double>
        {
            [-1] = 0.0,
            [0] = 0.0,
            [1] = 0.0
        };

        var counts = new Dictionary<int, int>
        {
            [-1] = 0,
            [0] = 0,
            [1] = 0
        };

        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            sums[state] += returns[i];
            counts[state]++;
        }

        var result = new Dictionary<int, double>(3);
        foreach (var state in StateValues)
        {
            result[state] = counts[state] == 0 ? 0.0 : sums[state] / counts[state];
        }

        return result;
    }

    private static string BuildPattern(IReadOnlyList<int> states, int start, int memory)
    {
        return string.Join('|', states.Skip(start).Take(memory));
    }

    private static int ToIndex(int state)
    {
        return state switch
        {
            -1 => 0,
            0 => 1,
            1 => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(state), "Состояние должно быть -1, 0 или 1.")
        };
    }

    private static double[] ApplyLaplace(IReadOnlyList<double> counts, double alpha)
    {
        var adjusted = counts.Select(x => x + alpha).ToArray();
        var total = adjusted.Sum();

        if (total <= 0)
        {
            return new[] { 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0 };
        }

        return adjusted.Select(x => x / total).ToArray();
    }
}
