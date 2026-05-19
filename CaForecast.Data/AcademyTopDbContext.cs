using CaForecast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data;

public sealed class AcademyTopDbContext(DbContextOptions<AcademyTopDbContext> options) : DbContext(options)
{
    public DbSet<CourseDirection> CourseDirections => Set<CourseDirection>();

    public DbSet<HistoricalMetric> HistoricalMetrics => Set<HistoricalMetric>();

    public DbSet<ModelSetting> ModelSettings => Set<ModelSetting>();

    public DbSet<PredictionResult> PredictionResults => Set<PredictionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseDirection>(entity =>
        {
            entity.ToTable("course_directions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<HistoricalMetric>(entity =>
        {
            entity.ToTable("historical_metrics");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DirectionId).HasColumnName("direction_id");
            entity.Property(x => x.MetricDate).HasColumnName("metric_date");
            entity.Property(x => x.MetricValue).HasColumnName("metric_value");
            entity.HasIndex(x => new { x.DirectionId, x.MetricDate }).IsUnique();
            entity.HasOne(x => x.Direction)
                .WithMany(x => x.HistoricalMetrics)
                .HasForeignKey(x => x.DirectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModelSetting>(entity =>
        {
            entity.ToTable("model_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MemoryDepthM).HasColumnName("memory_depth_m");
            entity.Property(x => x.ThresholdK).HasColumnName("threshold_k");
            entity.Property(x => x.SmoothingAlpha).HasColumnName("smoothing_alpha");
        });

        modelBuilder.Entity<PredictionResult>(entity =>
        {
            entity.ToTable("prediction_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SettingId).HasColumnName("setting_id");
            entity.Property(x => x.DirectionId).HasColumnName("direction_id");
            entity.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
            entity.Property(x => x.Mae).HasColumnName("mae");
            entity.Property(x => x.Rmse).HasColumnName("rmse");
            entity.Property(x => x.Mape).HasColumnName("mape");
            entity.Property(x => x.PredictedValuesJson).HasColumnName("predicted_values_json");
            entity.HasOne(x => x.Direction)
                .WithMany(x => x.PredictionResults)
                .HasForeignKey(x => x.DirectionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Setting)
                .WithMany(x => x.PredictionResults)
                .HasForeignKey(x => x.SettingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
