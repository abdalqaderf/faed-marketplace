using Faed.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.Infrastructure.Identity;

/// <summary>
/// Idempotent seeding of the fixed Faed Identity roles (Buyer, Merchant, Admin).
/// Safe to run on every startup. No user accounts are seeded here; a development
/// admin (if ever added) must take its password from configuration, never source
/// control (docs/08-SECURITY-AND-PRIVACY.md §12).
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(IdentityDataSeeder).FullName!);

        foreach (var role in FaedRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (result.Succeeded)
            {
                logger.LogInformation("Seeded Identity role {Role}", role);
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed Identity role '{role}': {errors}");
            }
        }
    }
}
