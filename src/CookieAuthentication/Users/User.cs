namespace CookieAuthentication.Users;

/// <summary>A user as the store holds it. <paramref name="PasswordHash"/> is never a plaintext password.</summary>
public sealed record User(string Id, string Username, string PasswordHash, string[] Roles);
