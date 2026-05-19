namespace CaForecast.Data.Entities;

public sealed class CourseDirection
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HistoricalMetric> HistoricalMetrics { get; set; } = new List<HistoricalMetric>();

    public ICollection<PredictionResult> PredictionResults { get; set; } = new List<PredictionResult>();
}
