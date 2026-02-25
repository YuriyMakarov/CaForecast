using System;
using System.Collections.Generic;

namespace CaForecast.Core;

public class ReturnCalculator
{
    public IReadOnlyList<double> CalculateLogReturns(IReadOnlyList<double> prices)
    {
        if (prices is null)
        {
            throw new ArgumentNullException(nameof(prices));
        }

        if (prices.Count < 2)
        {
            throw new ArgumentException("Требуется минимум две цены.", nameof(prices));
        }

        var returns = new List<double>(prices.Count - 1);
        for (var i = 1; i < prices.Count; i++)
        {
            var previous = prices[i - 1];
            var current = prices[i];

            if (previous <= 0 || current <= 0)
            {
                throw new ArgumentException("Цены должны быть положительными для расчета лог-доходности.", nameof(prices));
            }

            returns.Add(Math.Log(current / previous));
        }

        return returns;
    }
}
