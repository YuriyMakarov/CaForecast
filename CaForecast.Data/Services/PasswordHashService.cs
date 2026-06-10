using System.Security.Cryptography;
using System.Text;

namespace CaForecast.Data.Services;

public sealed class PasswordHashService
{
    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(passwordHash);

        var actualHash = HashPassword(password);
        var actualBytes = Encoding.UTF8.GetBytes(actualHash);
        var expectedBytes = Encoding.UTF8.GetBytes(passwordHash.ToLowerInvariant());

        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
