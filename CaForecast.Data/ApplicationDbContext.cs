using CaForecast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data;

public class ApplicationDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Specialty> Specialties => Set<Specialty>();

    public DbSet<Applicant> Applicants => Set<Applicant>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<CourseDirection> CourseDirections => Set<CourseDirection>();

    public DbSet<HistoricalMetric> HistoricalMetrics => Set<HistoricalMetric>();

    public DbSet<ModelSetting> ModelSettings => Set<ModelSetting>();

    public DbSet<PredictionResult> PredictionResults => Set<PredictionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCrm(modelBuilder);
        ConfigureForecasting(modelBuilder);
    }

    private static void ConfigureCrm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.Login).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.Login).IsUnique();
            entity.HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Specialty>(entity =>
        {
            entity.ToTable("specialties");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TuitionPrice).HasColumnName("tuition_price").HasColumnType("numeric(12,2)");
            entity.Property(x => x.DurationMonthsAfter9).HasColumnName("duration_months_after_9");
            entity.Property(x => x.DurationMonthsAfter11).HasColumnName("duration_months_after_11");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Applicant>(entity =>
        {
            entity.ToTable("applicants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.MiddleName).HasColumnName("middle_name").HasMaxLength(100);
            entity.Property(x => x.BirthDate).HasColumnName("birth_date");
            entity.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.ParentPhone).HasColumnName("parent_phone").HasMaxLength(30);
            entity.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.Phone);
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("applications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicantId).HasColumnName("applicant_id");
            entity.Property(x => x.SpecialtyId).HasColumnName("specialty_id");
            entity.Property(x => x.ManagerId).HasColumnName("manager_id");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.EducationBase).HasColumnName("education_base").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.StatusChangedAt).HasColumnName("status_changed_at");
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasOne(x => x.Applicant)
                .WithMany(x => x.Applications)
                .HasForeignKey(x => x.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Specialty)
                .WithMany(x => x.Applications)
                .HasForeignKey(x => x.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Manager)
                .WithMany(x => x.AssignedApplications)
                .HasForeignKey(x => x.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("contracts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicantId).HasColumnName("applicant_id");
            entity.Property(x => x.ApplicationId).HasColumnName("application_id");
            entity.Property(x => x.SpecialtyId).HasColumnName("specialty_id");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(x => x.Number).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SignedDate).HasColumnName("signed_date");
            entity.Property(x => x.TuitionPriceFixed).HasColumnName("tuition_price_fixed").HasColumnType("numeric(12,2)");
            entity.Property(x => x.DurationMonths).HasColumnName("duration_months");
            entity.Property(x => x.EducationBase).HasColumnName("education_base").HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.EducationStatus).HasColumnName("education_status").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => x.SignedDate);
            entity.HasOne(x => x.Applicant)
                .WithMany(x => x.Contracts)
                .HasForeignKey(x => x.ApplicantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Application)
                .WithOne(x => x.Contract)
                .HasForeignKey<Contract>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Specialty)
                .WithMany(x => x.Contracts)
                .HasForeignKey(x => x.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany(x => x.CreatedContracts)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContractId).HasColumnName("contract_id");
            entity.Property(x => x.Amount).HasColumnType("numeric(12,2)");
            entity.Property(x => x.PaidAt).HasColumnName("paid_at");
            entity.Property(x => x.Method).HasMaxLength(50);
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.PaidAt);
            entity.HasOne(x => x.Contract)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureForecasting(ModelBuilder modelBuilder)
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
            entity.Property(x => x.SpecialtyId).HasColumnName("specialty_id");
            entity.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
            entity.Property(x => x.PeriodFrom).HasColumnName("period_from");
            entity.Property(x => x.PeriodTo).HasColumnName("period_to");
            entity.Property(x => x.Mae).HasColumnName("mae");
            entity.Property(x => x.Rmse).HasColumnName("rmse");
            entity.Property(x => x.Mape).HasColumnName("mape");
            entity.Property(x => x.PredictedValuesJson).HasColumnName("predicted_values_json");
            entity.HasOne(x => x.Direction)
                .WithMany(x => x.PredictionResults)
                .HasForeignKey(x => x.DirectionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Specialty)
                .WithMany()
                .HasForeignKey(x => x.SpecialtyId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Setting)
                .WithMany(x => x.PredictionResults)
                .HasForeignKey(x => x.SettingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
