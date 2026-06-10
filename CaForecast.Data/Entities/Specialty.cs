namespace CaForecast.Data.Entities;

public sealed class Specialty
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal TuitionPrice { get; set; }

    public int DurationMonthsAfter9 { get; set; }

    public int DurationMonthsAfter11 { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Application> Applications { get; set; } = new List<Application>();

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
