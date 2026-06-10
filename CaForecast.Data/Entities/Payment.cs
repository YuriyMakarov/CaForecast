namespace CaForecast.Data.Entities;

public sealed class Payment
{
    public int Id { get; set; }

    public int ContractId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaidAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string? Method { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract? Contract { get; set; }
}
