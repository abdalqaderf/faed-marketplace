using Faed.Application.Abstractions;
using Faed.Infrastructure.Identity;
using Faed.Infrastructure.Persistence;
using Faed.Infrastructure.Storage;
using Faed.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Faed.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and supporting infrastructure services.
    /// Identity UI/authentication wiring stays in the Web composition root because it
    /// is an HTTP concern (docs/06-ARCHITECTURE.md §3).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        AddPrivateFileStorage(services, configuration, environment);

        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IClock, SystemClock>();

        return services;
    }

    private static void AddPrivateFileStorage(
        IServiceCollection services,
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
