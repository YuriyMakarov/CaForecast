namespace CaForecast.Data.Entities;

public sealed class Application
{
    public int Id { get; set; }

    public int ApplicantId { get; set; }

    public int SpecialtyId { get; set; }

    public int? ManagerId { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;

    public EducationBase EducationBase { get; set; } = EducationBase.Grade9;

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StatusChangedAt { get; set; }

    public string? Comment { get; set; }

    public Applicant? Applicant { get; set; }

    public Specialty? Specialty { get; set; }

    public User? Manager { get; set; }

    public Contract? Contract { get; set; }
}
