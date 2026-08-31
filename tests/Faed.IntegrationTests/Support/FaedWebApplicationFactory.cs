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
public sealed class FaedWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = TestSqlServer.WebDatabaseName;

    public string ConnectionString { get; } = TestSqlServer.ConnectionStringFor(DatabaseName);

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
