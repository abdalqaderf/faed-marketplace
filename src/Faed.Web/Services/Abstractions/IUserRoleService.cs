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

    /// <summary>
    /// True when <paramref name="userId"/> resolves to a real account that currently holds
    /// <paramref name="role"/>. Used by services for a defence-in-depth authorization recheck
    /// that does not rely on the MVC pipeline alone (docs/08-SECURITY-AND-PRIVACY.md §2).
    /// </summary>
    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
}
