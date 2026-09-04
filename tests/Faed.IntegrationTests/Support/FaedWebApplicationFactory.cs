using Faed.Web.Services.Abstractions;
using Faed.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// Hosts Faed.Web against a disposable SQL Server test database with a test authentication
/// scheme. The database is migrated in the constructor, before the host (and its role
/// seeding) starts.
/// </summary>
public class FaedWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The disposable test catalog this factory owns. Overridable so a test that
    /// drives a large end-to-end scenario can run against its own database.</summary>
    protected virtual string DatabaseName => TestSqlServer.WebDatabaseName;

    public string ConnectionString => TestSqlServer.ConnectionStringFor(DatabaseName);

    public bool DatabaseReady { get; private set; }

    public async Task InitializeAsync() => await InitializeDatabaseAsync();

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    private async Task InitializeDatabaseAsync()
    {
        if (!await TestSqlServer.IsReachableAsync(ConnectionString))
        {
            if (TestSqlServer.RunningInContinuousIntegration)
            {
                // CI must not report green while the web integration tests silently skip
                // (docs/09-TEST-STRATEGY.md §2). Fail the whole collection instead.
                throw new InvalidOperationException(
                    "CI=true but no SQL Server is reachable for the web integration tests. " +
                    "Provide a SQL Server service or set Faed_TEST_CONNECTION.");
            }

            if (TestSqlServer.WasExplicitlyConfigured)
            {
                // The developer said where the test server is; not reaching it is a real
                // failure, not an environment that simply lacks SQL Server.
                throw new InvalidOperationException(
                    "Faed_TEST_CONNECTION is set but its SQL Server is not reachable.");
            }

            DatabaseReady = false;
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var context = new ApplicationDbContext(options);
        TestSqlServer.AssertSafeTestDatabase(ConnectionString, DatabaseName);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        DatabaseReady = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Faed:AdminSeed:Email"] = null,
                ["Faed:AdminSeed:Password"] = null,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Defence in depth against a future eager connection-string read in the app's
            // composition root: bind the context to the disposable test catalog here as well,
            // so the suite can never write to the application database
            // (docs/09-TEST-STRATEGY.md §2). `DatabaseTargetsDisposableCatalog` proves it.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));

            services.AddControllers().AddApplicationPart(typeof(SellingProbeController).Assembly);

            services.RemoveAll(typeof(IFileStorage));
            services.AddSingleton<InMemoryFileStorage>();
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<InMemoryFileStorage>());

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && DatabaseReady)
        {
            TestSqlServer.AssertSafeTestDatabase(ConnectionString, DatabaseName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}
