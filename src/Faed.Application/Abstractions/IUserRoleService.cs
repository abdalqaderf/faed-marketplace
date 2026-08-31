namespace Faed.Application.Abstractions;

/// <summary>
/// Application-facing seam over ASP.NET Core Identity role management. Roles are an
/// Identity concern owned by Infrastructure; application services only express intent
/// (docs/06-ARCHITECTURE.md §3, docs/08-SECURITY-AND-PRIVACY.md §1).
/// </summary>
public interface IUserRoleService
{
    Task AddToRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    Task RemoveFromRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
}
