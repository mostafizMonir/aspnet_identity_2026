using CookieAuthentication.Authentication;
using CookieAuthentication.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();

// ---------------------------------------------------------------------------------------------
// Server-side session storage.
//
// AddDistributedMemoryCache is a distributed cache in name only -- it lives in this process, so
// sessions vanish on restart and are not shared between instances. It keeps the sample runnable
// with no infrastructure. For anything real, replace this one line and nothing else changes:
//
//   builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
//
// The ticket store below talks to IDistributedCache, so it does not care which one is registered.
// ---------------------------------------------------------------------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__monir_cookie_id";

        // Not readable from JavaScript, so XSS cannot exfiltrate the session id.
        options.Cookie.HttpOnly = true;

        // Always, so the cookie is never sent over plain HTTP. Localhost counts as a secure
        // context, so this still works over http in dev.
        //
        // Note the cookie name carries no __Host- prefix. That prefix is a browser-enforced
        // guarantee of Secure + Path=/ + no Domain; without it the same settings are applied
        // below, but nothing stops a subdomain from overwriting the cookie.
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Lax lets the cookie ride ordinary top-level navigations back to the site but not
        // cross-site POSTs, which blunts CSRF. Use None (plus CSRF tokens) only if a separate
        // front-end origin needs it.
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(1);

        // Renews the ticket when it is used past halfway, so an active user is not logged out
        // mid-session. Renewal rewrites the server-side entry via ITicketStore.RenewAsync.
        options.SlidingExpiration = true;

        // The cookie handler defaults to redirecting browsers to a login page. An API has no login
        // page, so answer with status codes instead of 302s.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Assigning SessionStore is what moves the ticket off the browser and onto the server: without it
// the cookie carries every claim, with it the cookie carries only a session id.
//
// It is set here rather than inside AddCookie(...) because the store has to be resolved from DI.
// Doing that in the AddCookie callback would mean calling BuildServiceProvider() and creating a
// second container -- this post-configuration hook gets the real one injected instead.
builder.Services
    .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CookieAuthentication v1"));
}

// Order matters: authentication resolves the cookie into HttpContext.User, and authorization then
// decides whether that user may proceed. Both must sit before endpoint execution.
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();

// A hand-driven demo page for exercising the flow in a real browser, where the cookie is actually
// stored and re-sent automatically. Served from the same origin as the API on purpose: a
// cross-origin page would need CORS with AllowCredentials and a SameSite=None cookie.
app.MapGet("/loginform", (IWebHostEnvironment environment) =>
        Results.File(Path.Combine(environment.WebRootPath, "loginform.html"), "text/html"))
    .AllowAnonymous()
    .ExcludeFromDescription();

// A protected resource, to show the cookie doing its job on an ordinary endpoint.
app.MapGet("/protected", (HttpContext http) =>
        Results.Ok($"Hello {http.User.Identity?.Name}. This came from a server-side session."))
    .RequireAuthorization()
    .WithTags("Demo");

// Restricted further by role -- the roles live in the server-side ticket, not in the cookie.
app.MapGet("/admin", () => Results.Ok("Admins only."))
    .RequireAuthorization(policy => policy.RequireRole("admin"))
    .WithTags("Demo");

app.Run();
