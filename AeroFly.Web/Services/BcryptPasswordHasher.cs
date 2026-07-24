using AeroFly.Web.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace AeroFly.Web.Services;

public class BcryptPasswordHasher : IPasswordHasher<User>
{
    public string HashPassword(User user, string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        if (hashedPassword.StartsWith("$2", StringComparison.Ordinal))
        {
            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }

        // Existing SHA-256 accounts are upgraded to BCrypt after their next successful login.
        using var sha = SHA256.Create();
        var legacyHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(providedPassword)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legacyHash),
            Encoding.UTF8.GetBytes(hashedPassword))
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }
}
