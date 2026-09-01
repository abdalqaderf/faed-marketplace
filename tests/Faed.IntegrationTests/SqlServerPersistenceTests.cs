using Faed.Web.Data;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Faed.IntegrationTests;

/// <summary>
/// Proves the configured SQL Server persistence can connect and apply the schema from
/// an empty database (TASK-001 Phase 5, docs/09-TEST-STRATEGY.md §2).
///
/// EF Core InMemory / SQLite are deliberately NOT used: concurrency and schema fidelity
/// must be proven against real SQL Server.
///
/// This test CREATES and DROPS its target database, so it uses its own
/// <c>Faed_TEST_CONNECTION</c> environment variable (never the application's
/// <c>ConnectionStrings__DefaultConnection</c>). Its catalog is always replaced with the
/// hard-coded <c>Faed_IntegrationTests</c> catalog before any create/drop operation. When
/// that variable is not set the test falls back to LocalDB. An unreachable SQL Server
/// skips the test on a developer workstation but fails it on CI (<c>CI=true</c>), so a
/// green CI run cannot silently omit the SQL Server proof.
/// </summary>
public class SqlServerPersistenceTests
{
    [SkippableFact]
    public async Task Migrations_ApplyFromEmptyDatabase_AndIdentitySchemaIsQueryable()
    {
        var connectionString = TestSqlServer.ConnectionStringFor(TestSqlServer.PersistenceDatabaseName);
        TestSqlServer.AssertSafeTestDatabase(connectionString, TestSqlServer.PersistenceDatabaseName);

        if (!await TestSqlServer.IsReachableAsync(connectionString))
        {
            // Skipping is only correct when the environment simply has no SQL Server and was
            // never told where to find one. On CI, or when the developer explicitly pointed
            // Faed_TEST_CONNECTION at a server, an unreachable server is a real failure —
            // otherwise a typo in that variable silently removes the whole SQL Server proof
            // (docs/09-TEST-STRATEGY.md §2).
            Skip.If(
                !TestSqlServer.RunningInContinuousIntegration && !TestSqlServer.WasExplicitlyConfigured,
                "No SQL Server reachable for integration testing (start LocalDB or set Faed_TEST_CONNECTION).");

            Assert.Fail(TestSqlServer.RunningInContinuousIntegration
                ? "CI=true but no SQL Server is reachable for integration testing. " +
                  "Provide a SQL Server service or set Faed_TEST_CONNECTION."
                : "Faed_TEST_CONNECTION is set but its SQL Server is not reachable.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name))
            // This test constructs the context without the web host's Identity DI, so the
            // ASP.NET Core Identity store options (which shorten a few key columns) are not
            // applied and the in-memory model differs cosmetically from the migration
            // snapshot. Snapshot fidelity is covered separately by
            // `dotnet ef migrations has-pending-model-changes`; here we only prove that the
            // real migration applies against real SQL Server.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var context = new ApplicationDbContext(options);

        try
        {
            // Start from a genuinely empty database.
            TestSqlServer.AssertSafeTestDatabase(connectionString, TestSqlServer.PersistenceDatabaseName);
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();

            Assert.True(await context.Database.CanConnectAsync());
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());

            // The Identity store is usable (0 rows in a fresh DB, but the query must succeed).
            Assert.Equal(0, await context.Users.CountAsync());
        }
        finally
        {
            TestSqlServer.AssertSafeTestDatabase(connectionString, TestSqlServer.PersistenceDatabaseName);
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Theory]
    [InlineData("Production_test")]
    [InlineData("Faed")]
    [InlineData("")]
    public void ConnectionStringFor_RejectsEveryDatabaseOutsideTheExplicitAllowList(string databaseName)
    {
        Assert.Throws<ArgumentException>(() => TestSqlServer.ConnectionStringFor(databaseName));
    }

    [Fact]
    public void DestructiveGuard_RejectsMismatchedCatalogAndAttachedDatabaseFiles()
    {
        Assert.Throws<InvalidOperationException>(() => TestSqlServer.AssertSafeTestDatabase(
            "Server=(localdb)\\MSSQLLocalDB;Database=Faed;Trusted_Connection=True",
            TestSqlServer.PersistenceDatabaseName));

        Assert.Throws<InvalidOperationException>(() => TestSqlServer.AssertSafeTestDatabase(
            $"Server=(localdb)\\MSSQLLocalDB;Database={TestSqlServer.PersistenceDatabaseName};" +
            "AttachDbFilename=production.mdf;Trusted_Connection=True",
            TestSqlServer.PersistenceDatabaseName));
    }
}
