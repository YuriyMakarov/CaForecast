namespace CaForecast.Data.Entities;

public sealed class Applicant
{
    public int Id { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? ParentPhone { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Application> Applications { get; set; } = new List<Application>();

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
