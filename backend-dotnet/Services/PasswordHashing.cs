using System.Security.Cryptography;
using System.Text;

namespace Mozaika.Api.Services;

public static class PasswordHashing
{
    public const int DefaultIterations = 120_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public static (string HashBase64, string SaltBase64, int Iterations) CreateHash(
        string password,
        int iterations = DefaultIterations
    )
    {
        var normalizedPassword = password ?? string.Empty;
        var normalizedIterations = Math.Clamp(iterations, 50_000, 500_000);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(normalizedPassword),
            salt,
            normalizedIterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes
        );

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), normalizedIterations);
    }

    public static bool Verify(
        string password,
        string hashBase64,
        string saltBase64,
        int iterations
    )
    {
        if (string.IsNullOrWhiteSpace(hashBase64) || string.IsNullOrWhiteSpace(saltBase64))
        {
            return false;
        }

        byte[] expectedHash;
        byte[] salt;
        try
        {
            expectedHash = Convert.FromBase64String(hashBase64);
            salt = Convert.FromBase64String(saltBase64);
        }
        catch
        {
            return false;
        }

        var normalizedIterations = Math.Clamp(iterations, 50_000, 500_000);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? string.Empty),
            salt,
            normalizedIterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length
        );

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
