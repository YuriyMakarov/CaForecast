namespace CaForecast.Data.Services;

public sealed record AuthenticatedUser(int Id, int RoleId, string RoleName, string Login, string FullName);
