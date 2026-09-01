using Faed.Web.Data;
using Faed.Web.Services;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Merchants;
using Faed.Web.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web;

/// <summary>
/// Composition root helpers for the single-project MVC application (docs/06-ARCHITECTURE.md).
/// Business services, persistence and supporting infrastructure are all registered here;
/// there are no separate Domain/Application/Infrastructure projects.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers use-case services, EF Core persistence and supporting infrastructure.</summary>
    public static IServiceCollection AddFaedPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddPersistence();
        services.AddPrivateFileStorage(configuration, environment);

        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IClock, SystemClock>();

        // Merchant verification use cases (docs/10-IMPLEMENTATION-PLAN.md Phase 1).
        services.AddOptions<MerchantVerificationOptions>()
            .Bind(configuration.GetSection(MerchantVerificationOptions.SectionName));
        services.AddScoped<IMerchantVerificationService, MerchantVerificationService>();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services)
    {
        // One application DbContext; Identity shares it. Migrations live in this project
        // under Data/Migrations (docs/06-ARCHITECTURE.md §5).
        //
        // The connection string is resolved from the *built* IConfiguration when the context
        // options are created, never captured here at registration time. Reading it eagerly
        // silently ignored any configuration source added after AddFaedPlatform runs — which
        // is exactly how a test host overrides it — and pointed the integration test host at
        // the application database instead of its disposable one (docs/09-TEST-STRATEGY.md §2).
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseSqlServer(ResolveConnectionString(
                serviceProvider.GetRequiredService<IConfiguration>())));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
    }

    private static string ResolveConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    private static void AddPrivateFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<LocalFileStorageOptions>()
            .Bind(configuration.GetSection(LocalFileStorageOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.LocalRootPath))
                {
                    options.LocalRootPath = Path.Combine(environment.ContentRootPath, "App_Data", "private-storage");
                }

                var resolvedRoot = Path.GetFullPath(options.LocalRootPath);
                var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
                if (IsWithin(resolvedRoot, webRoot) || PathsEqual(resolvedRoot, webRoot))
                {
                    throw new InvalidOperationException(
                        $"FileStorage:LocalRootPath ('{resolvedRoot}') must not be inside the web root. " +
                        "Verification documents are private (docs/08-SECURITY-AND-PRIVACY.md §3).");
                }

                options.LocalRootPath = resolvedRoot;
            });

        if (environment.IsProduction())
        {
            // LocalFileStorage is a development-only convenience. Production must bind a
            // real private object store to IFileStorage (docs/06-ARCHITECTURE.md §8);
            // fail loudly the first time a verification document is stored or read rather
            // than silently writing to ephemeral local disk.
            services.AddSingleton<IFileStorage>(_ => throw new InvalidOperationException(
                "No production IFileStorage is configured. LocalFileStorage is development-only; " +
                "register a cloud object storage implementation for this environment."));
            return;
        }

        services.AddSingleton<IFileStorage, LocalFileStorage>();
    }

    private static bool IsWithin(string candidate, string directory)
    {
        var directoryWithSeparator = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

        return candidate.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
