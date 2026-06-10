namespace CaForecast.Data.Entities;

public sealed class User
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public string Login { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role? Role { get; set; }

    public ICollection<Application> AssignedApplications { get; set; } = new List<Application>();

    public ICollection<Contract> CreatedContracts { get; set; } = new List<Contract>();
}
