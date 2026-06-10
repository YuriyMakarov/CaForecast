using Microsoft.EntityFrameworkCore;

namespace CaForecast.Data.Services;

public sealed class AuthenticationService(
    IDbContextFactory<AcademyTopDbContext> dbContextFactory,
    PasswordHashService passwordHashService)
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var normalizedLogin = login.Trim();

            var user = await dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .SingleOrDefaultAsync(x => x.Login == normalizedLogin && x.IsActive, cancellationToken);

            if (user?.Role is null || !passwordHashService.VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            return new AuthenticatedUser(user.Id, user.RoleId, user.Role.Name, user.Login, user.FullName);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or TimeoutException)
        {
            return null;
        }
    }
}
