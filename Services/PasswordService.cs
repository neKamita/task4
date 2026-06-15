using System.Security.Cryptography;

namespace Task4.Services;

public class PasswordService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"v1:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split(':');
        if (parts.Length != 4 || parts[0] != "v1")
        {
            return false;
        }

        var iterations = int.Parse(parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        var oldKey = Convert.FromBase64String(parts[3]);
        var newKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, oldKey.Length);

        return CryptographicOperations.FixedTimeEquals(oldKey, newKey);
    }
}
