using System;
using System.Collections.Generic;
using System.Linq;

namespace CaForecast.Core;

public class CaForecaster
{
    public ForecastResult Forecast(
        IReadOnlyList<double> prices,
        IReadOnlyList<double> returns,
        IReadOnlyList<int> encodedStates,
        int trainReturnsCount,
        int memory,
        double alpha,
        CaRuleTrainer trainer,
        MetricsService metricsService)
    {
        if (prices is null)
        {
            throw new ArgumentNullException(nameof(prices));
        }

        if (returns is null)
        {
            throw new ArgumentNullException(nameof(returns));
        }

        if (encodedStates is null)
        {
            throw new ArgumentNullException(nameof(encodedStates));
        }

        if (trainer is null)
        {
            throw new ArgumentNullException(nameof(trainer));
        }

        if (metricsService is null)
        {
            throw new ArgumentNullException(nameof(metricsService));
        }

        if (returns.Count != encodedStates.Count)
        {
            throw new ArgumentException("Длины массивов доходностей и закодированных состояний должны совпадать.");
        }

        if (trainReturnsCount <= memory || trainReturnsCount >= returns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(trainReturnsCount), "Размер обучающей выборки должен быть больше памяти и меньше общего числа доходностей.");
        }

        var trainStates = encodedStates.Take(trainReturnsCount).ToArray();
        var trainReturns = returns.Take(trainReturnsCount).ToArray();
        var model = trainer.Train(trainStates, trainReturns, memory, alpha);

        var actualReturns = new List<double>();
        var predictedReturns = new List<double>();
        var actualPrices = new List<double>();
        var predictedPrices = new List<double>();

        var currentPredictedPrice = prices[trainReturnsCount];

        for (var t = trainReturnsCount; t < returns.Count; t++)
        {
            var pattern = BuildPattern(encodedStates, t - memory, memory);
            var probabilities = model.TransitionProbabilities.TryGetValue(pattern, out var p)
                ? p
                : model.GlobalDistribution;

            var predictedState = ArgMaxState(probabilities);
            var predictedReturn = model.MeanReturnsByState[predictedState];

            actualReturns.Add(returns[t]);
            predictedReturns.Add(predictedReturn);

            var actualPrice = prices[t + 1];
            actualPrices.Add(actualPrice);

            currentPredictedPrice *= Math.Exp(predictedReturn);
            predictedPrices.Add(currentPredictedPrice);
        }

        var mae = metricsService.CalculateMae(actualPrices, predictedPrices);
        var mse = metricsService.CalculateMse(actualPrices, predictedPrices);
        var rmse = metricsService.CalculateRmse(actualPrices, predictedPrices);
        var mapePercent = metricsService.CalculateMapePercent(actualPrices, predictedPrices);

        return new ForecastResult
        {
            Memory = memory,
            TrainReturnsCount = trainReturnsCount,
            ActualReturns = actualReturns,
            PredictedReturns = predictedReturns,
            ActualPrices = actualPrices,
            PredictedPrices = predictedPrices,
            Mae = mae,
            Mse = mse,
            Rmse = rmse,
            MapePercent = mapePercent
        };
    }

    private static string BuildPattern(IReadOnlyList<int> states, int start, int memory)
    {
        return string.Join('|', states.Skip(start).Take(memory));
    }

    private static int ArgMaxState(IReadOnlyList<double> probs)
    {
        var index = 0;
        var max = probs[0];
        for (var i = 1; i < probs.Count; i++)
        {
            if (probs[i] > max)
            {
                max = probs[i];
                index = i;
            }
        }

        return index switch
        {
            0 => -1,
            1 => 0,
            2 => 1,
            _ => 0
        };
    }
}
