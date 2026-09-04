using System.Security.Claims;
using CookieAuthentication.Contracts;
using CookieAuthentication.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CookieAuthentication.Authentication;

public static class AuthEndpoints
{
    /// <summary>
    /// Bangladesh Standard Time. Resolved by IANA id, which .NET maps to the matching Windows id
    /// automatically, so the same string works on the dev machine and in the Linux container.
    /// </summary>
    private static readonly TimeZoneInfo BangladeshTimeZone = ResolveBangladeshTimeZone();

    private static TimeZoneInfo ResolveBangladeshTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Falls back to a fixed offset if the host has no tz database -- globalization-invariant
            // mode, or a trimmed container image. Bangladesh has observed a constant UTC+6 since
            // 2010, so the two agree today; the lookup above is what would pick up a future change.
            return TimeZoneInfo.CreateCustomTimeZone("UTC+06", TimeSpan.FromHours(6), "UTC+06", "UTC+06");
        }
    }

    /// <summary>
    /// Re-expresses an instant in Bangladesh Standard Time. This shifts only the rendered offset,
    /// never the instant itself: 03:19:45+00:00 and 09:19:45+06:00 are the same moment.
    /// </summary>
    private static DateTimeOffset? ToBangladeshTime(DateTimeOffset? instant) =>
        instant is null ? null : TimeZoneInfo.ConvertTime(instant.Value, BangladeshTimeZone);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithSummary("Exchanges credentials for a session cookie.");

        group.MapPost("/logout", (Delegate)LogoutAsync)
            .RequireAuthorization()
            .WithSummary("Destroys the server-side session and clears the cookie.");

        group.MapGet("/me", (Delegate)GetCurrentUserAsync)
            .RequireAuthorization()
            .WithSummary("Reports the identity behind the current session cookie.");

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserStore users,
        HttpContext httpContext)
    {
        var user = await users.ValidateCredentialsAsync(request.Username, request.Password);
        if (user is null)
        {
            // Deliberately vague: saying "no such user" vs "wrong password" tells an attacker
            // which usernames are real.
            return Results.Problem(
                title: "Invalid credentials",
                detail: "The username or password is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // These claims are what gets stored server-side by the ticket store. Because they never
        // travel to the browser, the claim set can grow without bloating the cookie -- the cookie
        // stays a single session id either way.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            // IsPersistent=false yields a session cookie (gone when the browser closes);
            // true writes it to disk with the expiry below.
            IsPersistent = request.RememberMe,
            IssuedUtc = DateTimeOffset.UtcNow,
        };

        // SignInAsync is the call that runs the whole flow: it builds the ticket, hands it to the
        // ITicketStore, gets back a session id, and writes only that id into the Set-Cookie header.
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

        // Reported straight from the user rather than from httpContext.User: SignInAsync writes the
        // Set-Cookie header but does not populate User for the request that is already in flight.
        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Roles,
            ToBangladeshTime(properties.ExpiresUtc)));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        // Removes the ticket from the store *and* sends an expired Set-Cookie. The server-side
        // removal is the important half: it is what a stateless JWT cannot do.
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(HttpContext httpContext)
    {
        // Reaching here means the cookie's session id resolved to a live ticket in the store.
        // Re-authenticating is how we get at the ticket's AuthenticationProperties, which carry the
        // expiry -- HttpContext.User alone only exposes the claims.
        var result = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Ok(ToResponse(httpContext.User, ToBangladeshTime(result.Properties?.ExpiresUtc)));
    }

    private static CurrentUserResponse ToResponse(ClaimsPrincipal principal, DateTimeOffset? expiresAt) =>
        new(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.Identity?.Name ?? string.Empty,
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            expiresAt);
}
