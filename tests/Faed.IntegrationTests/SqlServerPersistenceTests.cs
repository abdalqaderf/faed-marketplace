using Faed.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
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
/// <c>ConnectionStrings__DefaultConnection</c>) and refuses to run unless the database
/// name identifies it as a disposable test database. When that variable is not set it
/// falls back to a local SQL Server LocalDB test database; if that is unreachable the
/// test is skipped. When the variable IS set, an unreachable server fails the test.
/// </summary>
public class SqlServerPersistenceTests
{
    private const string TestConnectionEnvVar = "Faed_TEST_CONNECTION";

    private const string DefaultLocalDbConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=Faed_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true";

    [SkippableFact]
    public async Task Migrations_ApplyFromEmptyDatabase_AndIdentitySchemaIsQueryable()
    {
        var configured = Environment.GetEnvironmentVariable(TestConnectionEnvVar);
        var wasExplicitlyConfigured = !string.IsNullOrWhiteSpace(configured);
        var connectionString = wasExplicitlyConfigured ? configured! : DefaultLocalDbConnection;

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        Assert.False(
            string.IsNullOrWhiteSpace(databaseName),
            $"{TestConnectionEnvVar} must specify a database (Initial Catalog).");

        // Hard guard: this test drops the target database. Never touch anything that is
        // not obviously a throwaway test database.
        if (!databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run the destructive persistence test against database '{databaseName}'. " +
                $"Point {TestConnectionEnvVar} at a database whose name contains 'test'.");
        }

        if (!await SqlServerIsReachableAsync(connectionString))
        {
            // No explicit test database configured => environment simply lacks SQL Server.
            Skip.If(!wasExplicitlyConfigured, "No local SQL Server instance reachable for integration testing.");

            // An explicitly configured SQL Server that cannot be reached is a real failure.
            Assert.Fail($"{TestConnectionEnvVar} is set but its SQL Server is not reachable.");
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
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();

            Assert.True(await context.Database.CanConnectAsync());
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());

            // The Identity store is usable (0 rows in a fresh DB, but the query must succeed).
            Assert.Equal(0, await context.Users.CountAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<bool> SqlServerIsReachableAsync(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 5,
            };

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
