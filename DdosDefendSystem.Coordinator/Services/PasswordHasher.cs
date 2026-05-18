using System.Security.Cryptography;
using System.Text;

namespace DdosDefendSystem.Coordinator.Services;

public static class PasswordHasher
{
    public static string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var combinedBytes = Encoding.UTF8.GetBytes(password + salt + "Secret_Salt_99");
        var hashBytes = sha256.ComputeHash(combinedBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string salt, string hashedPassword)
    {
        var inputHash = HashPassword(password, salt);
        return inputHash == hashedPassword;
    }
}