namespace Faed.Web.Services.Abstractions;

/// <summary>
/// Application-facing seam over ASP.NET Core Identity role management. Roles are an
/// Identity concern; use-case services only express role-assignment intent
/// (docs/06-ARCHITECTURE.md §3, docs/08-SECURITY-AND-PRIVACY.md §1).
/// </summary>
public interface IUserRoleService
{
    Task AddToRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    Task RemoveFromRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
}
