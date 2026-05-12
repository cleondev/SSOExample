using System.Security.Cryptography;
using System.Text;

namespace SsoExample.Api.Security;

public static class PasswordHasher
{
    public static string Hash(string password, string salt = "dev-salt-change-me")
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(salt),
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"pbkdf2-sha256${salt}${Convert.ToBase64String(bytes)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 3 || parts[0] != "pbkdf2-sha256")
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(password, parts[1])),
            Encoding.UTF8.GetBytes(storedHash));
    }
}
