using Faed.Application.Abstractions;
using Faed.Infrastructure.Persistence;
using Faed.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and supporting infrastructure services.
    /// Identity UI/authentication wiring stays in the Web composition root because it
    /// is an HTTP concern (docs/06-ARCHITECTURE.md §3).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name)));

        services.AddScoped<IClock, SystemClock>();

        return services;
    }
}
