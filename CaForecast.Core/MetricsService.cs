using System;
using System.Collections.Generic;

namespace CaForecast.Core;

public class MetricsService
{
    public double CalculateMae(IReadOnlyList<double> actual, IReadOnlyList<double> predicted)
    {
        Validate(actual, predicted);

        var sum = 0.0;
        for (var i = 0; i < actual.Count; i++)
        {
            sum += Math.Abs(actual[i] - predicted[i]);
        }

        return sum / actual.Count;
    }

    public double CalculateMse(IReadOnlyList<double> actual, IReadOnlyList<double> predicted)
    {
        Validate(actual, predicted);

        var sum = 0.0;
        for (var i = 0; i < actual.Count; i++)
        {
            var diff = actual[i] - predicted[i];
            sum += diff * diff;
        }

        return sum / actual.Count;
    }

    public double CalculateRmse(IReadOnlyList<double> actual, IReadOnlyList<double> predicted)
    {
        return Math.Sqrt(CalculateMse(actual, predicted));
    }

    public double CalculateMapePercent(IReadOnlyList<double> actual, IReadOnlyList<double> predicted)
    {
        Validate(actual, predicted);

        var sum = 0.0;
        var count = 0;
        for (var i = 0; i < actual.Count; i++)
        {
            var absActual = Math.Abs(actual[i]);
            if (absActual < 1e-12)
            {
                continue;
            }

            sum += Math.Abs((actual[i] - predicted[i]) / absActual);
            count++;
        }

        if (count == 0)
        {
            throw new ArgumentException("MAPE нельзя рассчитать, когда все фактические значения равны нулю.");
        }

        return 100.0 * (sum / count);
    }

    private static void Validate(IReadOnlyList<double> actual, IReadOnlyList<double> predicted)
    {
        if (actual is null)
        {
            throw new ArgumentNullException(nameof(actual));
        }

        if (predicted is null)
        {
            throw new ArgumentNullException(nameof(predicted));
        }

        if (actual.Count == 0 || predicted.Count == 0)
        {
            throw new ArgumentException("Входные коллекции не должны быть пустыми.");
        }

        if (actual.Count != predicted.Count)
        {
            throw new ArgumentException("Входные коллекции должны иметь одинаковую длину.");
        }
    }
}
