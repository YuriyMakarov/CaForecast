namespace CaForecast.Core;

public sealed class ForecastMetricsService
{
    public ForecastErrorMetrics Calculate(IReadOnlyList<double> actualValues, IReadOnlyList<double> predictedValues)
    {
        Validate(actualValues, predictedValues);

        var absoluteErrorSum = 0.0;
        var squaredErrorSum = 0.0;
        var mapeSum = 0.0;
        var mapeCount = 0;

        for (var index = 0; index < actualValues.Count; index++)
        {
            var error = actualValues[index] - predictedValues[index];
            absoluteErrorSum += Math.Abs(error);
            squaredErrorSum += error * error;

            var actual = actualValues[index];
            if (Math.Abs(actual) > 1e-12)
            {
                mapeSum += Math.Abs(error / actual);
                mapeCount++;
            }
        }

        var mse = squaredErrorSum / actualValues.Count;
        return new ForecastErrorMetrics
        {
            Mae = absoluteErrorSum / actualValues.Count,
            Mse = mse,
            Rmse = Math.Sqrt(mse),
            Mape = mapeCount == 0 ? 0.0 : (mapeSum / mapeCount) * 100.0
        };
    }

    private static void Validate(IReadOnlyList<double> actualValues, IReadOnlyList<double> predictedValues)
    {
        ArgumentNullException.ThrowIfNull(actualValues);
        ArgumentNullException.ThrowIfNull(predictedValues);

        if (actualValues.Count == 0 || predictedValues.Count == 0)
        {
            throw new ArgumentException("Нельзя вычислить метрики на пустой выборке.");
        }

        if (actualValues.Count != predictedValues.Count)
        {
            throw new ArgumentException("Длины фактического и прогнозного рядов должны совпадать.");
        }
    }
}
