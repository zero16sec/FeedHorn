using System.Security.Cryptography;
using System.Text;
using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedHorn.Services;

public class AuthService
{
    private readonly FeedHornContext _context;

    public AuthService(FeedHornContext context)
    {
        _context = context;
    }

    public async Task EnsureDefaultUserExists()
    {
        var userCount = await _context.Users.CountAsync();
        if (userCount == 0)
        {
            var defaultUser = new User
            {
                Username = "admin",
                PasswordHash = HashPassword("admin"),
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(defaultUser);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User?> ValidateCredentials(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return null;

        if (VerifyPassword(password, user.PasswordHash))
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return user;
        }

        return null;
    }

    public async Task<User?> GetUserById(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<bool> ChangePassword(int userId, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = HashPassword(newPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public string HashPassword(string password)
    {
        // Using PBKDF2 with SHA256
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        var hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);

        return Convert.ToBase64String(hashBytes);
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        var hashBytes = Convert.FromBase64String(storedHash);
        var salt = new byte[16];
        Array.Copy(hashBytes, 0, salt, 0, 16);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        for (int i = 0; i < 32; i++)
        {
            if (hashBytes[i + 16] != hash[i])
                return false;
        }

        return true;
    }
}
