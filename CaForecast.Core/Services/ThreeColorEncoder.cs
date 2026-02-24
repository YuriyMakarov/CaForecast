using System;
using System.Collections.Generic;

namespace CaForecast.Core;

public class ThreeColorEncoder
{
    public IReadOnlyList<int> Encode(IReadOnlyList<double> returns, double k)
    {
        if (returns is null)
        {
            throw new ArgumentNullException(nameof(returns));
        }

        if (k < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "k должен быть неотрицательным.");
        }

        var states = new List<int>(returns.Count);
        foreach (var value in returns)
        {
            if (value > k)
            {
                states.Add(1);
            }
            else if (value < -k)
            {
                states.Add(-1);
            }
            else
            {
                states.Add(0);
            }
        }

        return states;
    }
}
