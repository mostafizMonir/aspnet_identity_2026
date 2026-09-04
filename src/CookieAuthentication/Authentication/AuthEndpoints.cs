using System.Security.Claims;
using CookieAuthentication.Contracts;
using CookieAuthentication.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CookieAuthentication.Authentication;

public static class AuthEndpoints
{
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
            properties.ExpiresUtc));
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

        return Results.Ok(ToResponse(httpContext.User, result.Properties?.ExpiresUtc));
    }

    private static CurrentUserResponse ToResponse(ClaimsPrincipal principal, DateTimeOffset? expiresUtc) =>
        new(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.Identity?.Name ?? string.Empty,
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            expiresUtc);
}
