using Microsoft.AspNetCore.Identity;

namespace CookieAuthentication.Users;

/// <summary>
/// A hard-coded user list, seeded at construction. This exists so the cookie/session mechanics can
/// be demonstrated without dragging in a database -- it is NOT a template for a real user store.
/// Passwords are still hashed with <see cref="PasswordHasher{TUser}"/> rather than compared as
/// plaintext, because the verification path is the part worth copying.
/// </summary>
public sealed class InMemoryUserStore : IUserStore
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly Dictionary<string, User> _usersByUsername;

    public InMemoryUserStore()
    {
        // Demo credentials: alice/Password123! (admin) and bob/Password123! (member).
        _usersByUsername = new[]
        {
            CreateUser("1", "alice", "Password123!", ["admin", "member"]),
            CreateUser("2", "bob", "Password123!", ["member"]),
        }.ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);
    }

    public Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        if (!_usersByUsername.TryGetValue(username, out var user))
        {
            // A real store should still do a dummy hash comparison here so that a missing username
            // and a wrong password take the same amount of time (no user enumeration by timing).
            return Task.FromResult<User?>(null);
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        var isValid = result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;

        return Task.FromResult(isValid ? user : null);
    }

    public Task<User?> FindByIdAsync(string id) =>
        Task.FromResult(_usersByUsername.Values.FirstOrDefault(user => user.Id == id));

    private User CreateUser(string id, string username, string password, string[] roles)
    {
        var user = new User(id, username, PasswordHash: string.Empty, roles);
        return user with { PasswordHash = _hasher.HashPassword(user, password) };
    }
}
