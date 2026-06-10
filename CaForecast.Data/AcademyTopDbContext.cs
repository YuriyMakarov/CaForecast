using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data;

public sealed class AcademyTopDbContext(DbContextOptions<AcademyTopDbContext> options) : ApplicationDbContext(options)
{
}
