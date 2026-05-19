namespace CaForecast.Data.Entities;

public sealed class ModelSetting
{
    public int Id { get; set; }

    public int MemoryDepthM { get; set; }

    public double ThresholdK { get; set; }

    public double SmoothingAlpha { get; set; }

    public ICollection<PredictionResult> PredictionResults { get; set; } = new List<PredictionResult>();
}
