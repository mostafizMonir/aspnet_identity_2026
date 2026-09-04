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
public sealed record CurrentUserResponse(string Id, string Username, string[] Roles, DateTimeOffset? SessionExpiresUtc);
