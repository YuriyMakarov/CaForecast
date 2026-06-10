namespace CaForecast.Data.Entities;

public sealed class Contract
{
    public int Id { get; set; }

    public int ApplicantId { get; set; }

    public int ApplicationId { get; set; }

    public int SpecialtyId { get; set; }

    public int? CreatedByUserId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly SignedDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public decimal TuitionPriceFixed { get; set; }

    public int DurationMonths { get; set; }

    public EducationBase EducationBase { get; set; }

    public EducationStatus EducationStatus { get; set; } = EducationStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Applicant? Applicant { get; set; }

    public Application? Application { get; set; }

    public Specialty? Specialty { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
