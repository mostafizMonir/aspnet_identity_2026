namespace CookieAuthentication.Users;

/// <summary>
/// The seam between authentication and wherever users actually live. Swapping the in-memory
/// implementation for EF Core / ASP.NET Core Identity should not require touching the endpoints.
/// </summary>
public interface IUserStore
{
    /// <summary>Returns the user when the credentials are valid, otherwise <c>null</c>.</summary>
    Task<User?> ValidateCredentialsAsync(string username, string password);

    Task<User?> FindByIdAsync(string id);
}
