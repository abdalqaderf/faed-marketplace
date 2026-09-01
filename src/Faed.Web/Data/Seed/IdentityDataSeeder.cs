using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Data.Seed;

/// <summary>
/// Idempotent seeding of the fixed Faed Identity roles (Buyer, Merchant, Admin) and
/// optional development accounts.
///
/// Each account is seeded only when its email and password are supplied via user secrets
/// or environment variables, and the caller should only invoke it outside Production. No
/// password is ever stored in source control (docs/08-SECURITY-AND-PRIVACY.md §11-12).
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

    public static Task SeedDevelopmentAdminAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default) =>
        SeedDevelopmentAccountAsync(
            services,
            "AdminSeed",
            FaedRoles.Admin,
            cancellationToken);

    // A development seed for the other roles belongs to the phase that first needs one
    // (AGENTS.md §12 — do not scaffold future phases). The helper below is already generic,
    // so adding one is a single call.
    private static async Task SeedDevelopmentAccountAsync(
        IServiceProvider services,
        string configurationSection,
        string role,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = Logger(scope);

        var emailKey = $"Faed:{configurationSection}:Email";
        var passwordKey = $"Faed:{configurationSection}:Password";
        var email = configuration[emailKey];
        var password = configuration[passwordKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Development {Role} seed skipped: set {EmailKey} and {PasswordKey} (user secrets) to enable it.",
                role,
                emailKey,
                passwordKey);
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, role))
            {
                await AddToRoleAsync(userManager, existing, role);
            }

            logger.LogInformation("Development {Role} {Email} already present", role, email);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            var errors = string.Join(", ", created.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"Failed to seed development {role} '{email}': {errors}");
        }

        await AddToRoleAsync(userManager, user, role);
        logger.LogInformation("Seeded development {Role} {Email}", role, email);
    }

    private static async Task AddToRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role)
    {
        var result = await userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        throw new InvalidOperationException(
            $"Failed to add development account '{user.Email}' to role '{role}': {errors}");
    }

    private static ILogger Logger(IServiceScope scope) => scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(typeof(IdentityDataSeeder).FullName!);
}
