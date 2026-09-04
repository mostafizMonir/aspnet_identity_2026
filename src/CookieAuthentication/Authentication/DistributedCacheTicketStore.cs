using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace CookieAuthentication.Authentication;

/// <summary>
/// Makes cookie authentication <em>stateful</em>.
/// <para>
/// Out of the box, <c>AddCookie()</c> serialises the entire <see cref="AuthenticationTicket"/> --
/// every claim -- into the cookie itself. The browser carries the user's identity around, and the
/// server keeps nothing. That is convenient but it means the cookie grows with the claim set and
/// cannot be revoked before it expires.
/// </para>
/// <para>
/// Registering an <see cref="ITicketStore"/> as <see cref="CookieAuthenticationOptions.SessionStore"/>
/// flips that around. The ticket is stored here, server-side, under a generated key; the cookie ends
/// up holding only that key -- the session id. Every request the browser sends the cookie, the cookie
/// middleware calls <see cref="RetrieveAsync"/>, and the ticket is rehydrated from the store.
/// Signing out calls <see cref="RemoveAsync"/>, which genuinely revokes the session.
/// </para>
/// <para>
/// The backing store is <see cref="IDistributedCache"/>. In Development that is registered as an
/// in-memory cache, so sessions die with the process and do not survive a second instance. Point it
/// at Redis or SQL Server in production and the same code becomes shared, durable session state --
/// see <c>Program.cs</c> for where to swap it.
/// </para>
/// </summary>
public sealed class DistributedCacheTicketStore(IDistributedCache cache) : ITicketStore
{
    /// <summary>Namespaces session ids so the cache can be shared with other data.</summary>
    private const string KeyPrefix = "auth-session:";

    /// <summary>
    /// Stores a brand-new ticket and returns the key that becomes the cookie's payload.
    /// A GUID is used rather than anything derived from the user: the session id is a bearer
    /// credential, so it must be unguessable and must not leak who it belongs to.
    /// </summary>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    /// <summary>
    /// Writes (or overwrites) the ticket at <paramref name="key"/>. Called on sign-in and again
    /// whenever sliding expiration renews the ticket.
    /// </summary>
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new DistributedCacheEntryOptions();

        // Expire the server-side copy no later than the ticket itself, so a stale session cannot
        // outlive the cookie that points at it. Without an absolute expiry the cache entry would
        // linger forever once the cookie is gone.
        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.SetAbsoluteExpiration(expiresUtc.Value);
        }
        else
        {
            options.SetSlidingExpiration(TimeSpan.FromHours(1));
        }

        return cache.SetAsync(KeyPrefix + key, TicketSerializer.Default.Serialize(ticket), options);
    }

    /// <summary>
    /// Looks the ticket up by session id. Returning <c>null</c> -- because the entry expired or was
    /// removed by a sign-out -- makes the request anonymous even though the browser sent a cookie.
    /// </summary>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await cache.GetAsync(KeyPrefix + key);
        return bytes is null ? null : TicketSerializer.Default.Deserialize(bytes);
    }

    /// <summary>Drops the session server-side. This is what makes logout actually revoke access.</summary>
    public Task RemoveAsync(string key) => cache.RemoveAsync(KeyPrefix + key);
}
