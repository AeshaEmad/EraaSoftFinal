using AeroFly.Web.Models;
using AeroFly.Web.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace AeroFly.Web.Tests;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _passwordHasher = new();

    [Fact]
    public void HashPassword_UsesBcryptAndVerifiesCorrectPassword()
    {
        var user = CreateUser();
        var hash = _passwordHasher.HashPassword(user, "Secure@123");

        var result = _passwordHasher.VerifyHashedPassword(user, hash, "Secure@123");

        Assert.StartsWith("$2", hash);
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_AcceptsLegacyShaAndRequestsUpgrade()
    {
        var user = CreateUser();
        using var sha = SHA256.Create();
        var legacyHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes("Legacy@123")));

        var result = _passwordHasher.VerifyHashedPassword(user, legacyHash, "Legacy@123");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    private static User CreateUser()
    {
        return new User
        {
            FName = "Test",
            LName = "User",
            Email = "test@example.com",
            Password = string.Empty,
            CreatedDay = 1,
            CreatedMonth = 1,
            CreatedYear = 2026
        };
    }
}
