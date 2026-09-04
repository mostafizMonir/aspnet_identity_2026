using System.ComponentModel.DataAnnotations;

namespace CookieAuthentication.Contracts;

public sealed record LoginRequest
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }

    /// <summary>
    /// When false the cookie is a session cookie -- it dies with the browser. When true it is
    /// persisted to disk and survives a restart until it expires.
    /// </summary>
    public bool RememberMe { get; init; }
}

/// <summary>What <c>/auth/me</c> reports about the current session.</summary>
/// <param name="SessionExpiresAt">
/// When the session expires, expressed in Bangladesh Standard Time (UTC+6). It is a
/// <see cref="DateTimeOffset"/>, so the instant is unambiguous regardless of the offset it is
/// rendered in -- the offset only decides how it reads.
/// </param>
public sealed record CurrentUserResponse(
    string Id,
    string Username,
    string[] Roles,
    DateTimeOffset? SessionExpiresAt);
