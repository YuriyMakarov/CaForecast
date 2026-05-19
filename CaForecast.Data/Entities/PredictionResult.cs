namespace CaForecast.Data.Entities;

public sealed class PredictionResult
{
    public int Id { get; set; }

    public int SettingId { get; set; }

    public int DirectionId { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    public double Mae { get; set; }

    public double Rmse { get; set; }

    public double Mape { get; set; }

    public string PredictedValuesJson { get; set; } = "[]";

    public ModelSetting? Setting { get; set; }

    public CourseDirection? Direction { get; set; }
}
