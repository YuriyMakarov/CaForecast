namespace CaForecast.Core;

public sealed class SalesForecastService
{
    private static readonly int[] ProbabilityStateOrder = [-1, 0, 1];

    private readonly LogarithmicGrowthCalculator _growthCalculator = new();
    private readonly ThreeStateDiscretizer _discretizer = new();
    private readonly CellularAutomatonTrainer _trainer = new();
    private readonly ForecastMetricsService _metricsService = new();

    public ForecastScenarioResult BuildForecast(
        IReadOnlyList<double> seriesValues,
        IReadOnlyList<DateOnly?> periods,
        ForecastingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesValues);
        ArgumentNullException.ThrowIfNull(periods);
        ArgumentNullException.ThrowIfNull(parameters);

        ValidateArguments(seriesValues, periods, parameters);

        var returns = _growthCalculator.Calculate(seriesValues);
        var states = _discretizer.Discretize(returns, parameters.ThresholdK);
        var trainingReturnCount = CalculateTrainingReturnCount(returns.Count, parameters.TrainRatio, parameters.MemoryDepthM);

        var trainStates = states.Take(trainingReturnCount).ToArray();
        var trainReturns = returns.Take(trainingReturnCount).ToArray();
        var model = _trainer.Train(trainStates, trainReturns, parameters.MemoryDepthM, parameters.SmoothingAlpha);

        var forecastReturnCount = returns.Count - trainingReturnCount;
        var predictedReturns = new List<double>(forecastReturnCount);
        var predictedValues = new List<double>(forecastReturnCount);
        var actualReturns = new List<double>(forecastReturnCount);
        var actualValues = new List<double>(forecastReturnCount);
        var forecastPoints = new List<ForecastPoint>(forecastReturnCount);
        var recursiveStateHistory = trainStates.ToList();
        var currentPredictedValue = seriesValues[trainingReturnCount];
        var fallbackUsageCount = 0;

        for (var index = trainingReturnCount; index < returns.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pattern = CellularAutomatonTrainer.BuildPattern(
                recursiveStateHistory,
                recursiveStateHistory.Count - parameters.MemoryDepthM,
                parameters.MemoryDepthM);

            var isFallback = !model.TransitionProbabilities.TryGetValue(pattern, out var probabilities);
            probabilities ??= model.GlobalDistribution;

            if (isFallback)
            {
                fallbackUsageCount++;
            }

            var predictedReturn = CalculateExpectedReturn(probabilities, model.MeanReturnsByState);
            var predictedState = _discretizer.DiscretizeSingle(predictedReturn, parameters.ThresholdK);

            currentPredictedValue *= Math.Exp(predictedReturn);
            recursiveStateHistory.Add(predictedState);

            predictedReturns.Add(predictedReturn);
            predictedValues.Add(currentPredictedValue);
            actualReturns.Add(returns[index]);
            actualValues.Add(seriesValues[index + 1]);
            forecastPoints.Add(new ForecastPoint
            {
                Period = periods[index + 1],
                ActualValue = seriesValues[index + 1],
                PredictedValue = currentPredictedValue
            });
        }

        return new ForecastScenarioResult
        {
            Parameters = parameters,
            TrainingObservationCount = trainingReturnCount + 1,
            FallbackUsageCount = fallbackUsageCount,
            ActualReturns = actualReturns,
            PredictedReturns = predictedReturns,
            ActualValues = actualValues,
            PredictedValues = predictedValues,
            ForecastPoints = forecastPoints,
            Metrics = _metricsService.Calculate(actualValues, predictedValues)
        };
    }

    public MemoryOptimizationResult FindBestMemoryDepth(
        IReadOnlyList<double> seriesValues,
        IReadOnlyList<DateOnly?> periods,
        double thresholdK,
        double smoothingAlpha,
        double trainRatio,
        int minMemoryDepthM,
        int maxMemoryDepthM,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesValues);
        ArgumentNullException.ThrowIfNull(periods);

        if (minMemoryDepthM < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minMemoryDepthM), "Минимальная глубина памяти должна быть >= 1.");
        }

        if (maxMemoryDepthM < minMemoryDepthM)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemoryDepthM), "Максимальная глубина памяти должна быть не меньше минимальной.");
        }

        var candidates = new List<MemoryOptimizationCandidate>();
        ForecastScenarioResult? bestScenario = null;

        for (var memoryDepth = minMemoryDepthM; memoryDepth <= maxMemoryDepthM; memoryDepth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ForecastScenarioResult scenario;
            try
            {
                scenario = BuildForecast(
                    seriesValues,
                    periods,
                    new ForecastingParameters
                    {
                        MemoryDepthM = memoryDepth,
                        ThresholdK = thresholdK,
                        SmoothingAlpha = smoothingAlpha,
                        TrainRatio = trainRatio
                    },
                    cancellationToken);
            }
            catch (ArgumentException)
            {
                break;
            }

            candidates.Add(new MemoryOptimizationCandidate
            {
                MemoryDepthM = memoryDepth,
                Metrics = scenario.Metrics
            });

            if (bestScenario is null || scenario.Metrics.Rmse < bestScenario.Metrics.Rmse)
            {
                bestScenario = scenario;
            }
        }

        if (bestScenario is null)
        {
            throw new InvalidOperationException("Не удалось подобрать допустимую конфигурацию памяти для заданного ряда.");
        }

        return new MemoryOptimizationResult
        {
            BestMemoryDepthM = bestScenario.Parameters.MemoryDepthM,
            BestScenario = bestScenario,
            Candidates = candidates
        };
    }

    private static void ValidateArguments(
        IReadOnlyList<double> seriesValues,
        IReadOnlyList<DateOnly?> periods,
        ForecastingParameters parameters)
    {
        if (seriesValues.Count < 6)
        {
            throw new ArgumentException("Для прогнозирования требуется минимум 6 наблюдений.", nameof(seriesValues));
        }

        if (seriesValues.Count != periods.Count)
        {
            throw new ArgumentException("Количество периодов должно совпадать с количеством наблюдений.");
        }

        if (parameters.MemoryDepthM < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.MemoryDepthM), "Глубина памяти должна быть не меньше 1.");
        }

        if (parameters.ThresholdK < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.ThresholdK), "Порог k не может быть отрицательным.");
        }

        if (parameters.SmoothingAlpha < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.SmoothingAlpha), "Параметр alpha не может быть отрицательным.");
        }

        if (parameters.TrainRatio <= 0 || parameters.TrainRatio >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.TrainRatio), "TrainRatio должен находиться в диапазоне (0; 1).");
        }
    }

    private static int CalculateTrainingReturnCount(int totalReturnCount, double trainRatio, int memoryDepthM)
    {
        var trainingReturnCount = (int)Math.Round(totalReturnCount * trainRatio, MidpointRounding.AwayFromZero);
        trainingReturnCount = Math.Max(memoryDepthM + 1, trainingReturnCount);
        trainingReturnCount = Math.Min(trainingReturnCount, totalReturnCount - 1);

        if (trainingReturnCount <= memoryDepthM)
        {
            throw new ArgumentException("Обучающая часть ряда слишком короткая для выбранной глубины памяти.");
        }

        return trainingReturnCount;
    }

    private static double CalculateExpectedReturn(
        IReadOnlyList<double> probabilities,
        IReadOnlyDictionary<int, double> meanReturnsByState)
    {
        var expectedReturn = 0.0;
        for (var index = 0; index < ProbabilityStateOrder.Length; index++)
        {
            var state = ProbabilityStateOrder[index];
            expectedReturn += probabilities[index] * meanReturnsByState[state];
        }

        return expectedReturn;
    }
}
