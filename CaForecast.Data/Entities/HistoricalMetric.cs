namespace CaForecast.Data.Entities;

public sealed class HistoricalMetric
{
    public int Id { get; set; }

    public int DirectionId { get; set; }

    public DateOnly MetricDate { get; set; }

    public double MetricValue { get; set; }

    public CourseDirection? Direction { get; set; }
}
