using Faed.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.Infrastructure.Identity;

/// <summary>
/// Idempotent seeding of the fixed Faed Identity roles (Buyer, Merchant, Admin) and an
/// optional development admin account.
///
/// The admin account is seeded only when <c>Faed:AdminSeed:Email</c> and
/// <c>Faed:AdminSeed:Password</c> are both supplied via user secrets or environment
/// variables, and the caller should only invoke it outside Production. No password is
/// ever stored in source control (docs/08-SECURITY-AND-PRIVACY.md §11-12).
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = Logger(scope);

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

    public static async Task SeedDevelopmentAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = Logger(scope);

        var email = configuration["Faed:AdminSeed:Email"];
        var password = configuration["Faed:AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Development admin seed skipped: set Faed:AdminSeed:Email and Faed:AdminSeed:Password (user secrets) to enable it.");
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, FaedRoles.Admin))
            {
                await userManager.AddToRoleAsync(existing, FaedRoles.Admin);
            }

            logger.LogInformation("Development admin {Email} already present", email);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            var errors = string.Join(", ", created.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed development admin '{email}': {errors}");
        }

        await userManager.AddToRoleAsync(admin, FaedRoles.Admin);
        logger.LogInformation("Seeded development admin {Email}", email);
    }

    private static ILogger Logger(IServiceScope scope) => scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(typeof(IdentityDataSeeder).FullName!);
}
