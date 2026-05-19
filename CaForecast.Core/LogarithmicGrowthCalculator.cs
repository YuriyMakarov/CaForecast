using System.Globalization;

namespace CaForecast.Core;

public sealed class LogarithmicGrowthCalculator
{
    /// <summary>
    /// Рассчитывает логарифмические темпы прироста ряда:
    /// r_t = ln(S_t / S_{t-1}).
    /// </summary>
    public IReadOnlyList<double> Calculate(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 2)
        {
            throw new ArgumentException("Для расчета требуется минимум два наблюдения.", nameof(values));
        }

        var returns = new double[values.Count - 1];
        for (var index = 1; index < values.Count; index++)
        {
            var previous = values[index - 1];
            var current = values[index];

            if (previous <= 0 || current <= 0)
            {
                throw new ArgumentException(
                    $"Все значения ряда должны быть положительными. Ошибка на позиции {index.ToString(CultureInfo.InvariantCulture)}.",
                    nameof(values));
            }

            returns[index - 1] = Math.Log(current / previous);
        }

        return returns;
    }
}
