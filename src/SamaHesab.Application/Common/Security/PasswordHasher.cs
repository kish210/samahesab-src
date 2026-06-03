using System.Security.Cryptography;

namespace SamaHesab.Application.Common.Security;

/// <summary>PBKDF2 (SHA-256) password hashing — shared by desktop and API.</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int KeySize = 32;

    public static (string hash, string salt) Create(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(saltBytes));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expected = Convert.FromBase64String(hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}
