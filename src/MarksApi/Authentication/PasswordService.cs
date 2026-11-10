using MarksApi.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarksApi.Authentication;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
}

public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    private static readonly User DummyUser = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(DummyUser, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(DummyUser, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
