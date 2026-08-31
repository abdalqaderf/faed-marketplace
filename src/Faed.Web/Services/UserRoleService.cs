using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Faed.Web.Services;

/// <summary>
/// ASP.NET Core Identity implementation of <see cref="IUserRoleService"/>. Role assignment
/// is idempotent so approval/reinstatement can safely run more than once.
/// </summary>
public sealed class UserRoleService(UserManager<ApplicationUser> userManager) : IUserRoleService
{
    public async Task AddToRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        if (await userManager.IsInRoleAsync(user, role))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to add role '{role}' to user '{userId}': {Describe(result)}");
        }
    }

    public async Task RemoveFromRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        if (!await userManager.IsInRoleAsync(user, role))
        {
            return;
        }

        var result = await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to remove role '{role}' from user '{userId}': {Describe(result)}");
        }
    }

    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));
}
