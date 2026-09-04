using Microsoft.Data.SqlClient;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// Resolves a disposable SQL Server test database connection string and probes whether a
/// server is reachable (docs/09-TEST-STRATEGY.md §2 — concurrency and schema fidelity are
/// proven against real SQL Server, never InMemory/SQLite).
/// </summary>
public static class TestSqlServer
{
    private const string BaseConnectionEnvVar = "Faed_TEST_CONNECTION";

    public const string PersistenceDatabaseName = "Faed_IntegrationTests";

    public const string WebDatabaseName = "Faed_WebTests";

    /// <summary>
    /// A third disposable catalog for the demo-seed test, isolated from the shared
    /// <see cref="WebDatabaseName"/> catalog that the other web tests exercise.
    /// </summary>
    public const string DemoSeedDatabaseName = "Faed_DemoSeedTests";

    private static readonly HashSet<string> AllowedDatabaseNames =
        new(StringComparer.Ordinal) { PersistenceDatabaseName, WebDatabaseName, DemoSeedDatabaseName };

    private const string DefaultLocalDbConnection =
        $"Server=(localdb)\\MSSQLLocalDB;Database={PersistenceDatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true";

    public static bool WasExplicitlyConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BaseConnectionEnvVar));

    /// <summary>
    /// True on a continuous-integration runner (most CI systems set <c>CI=true</c>). There a
    /// missing SQL Server is a hard failure — the integration suite must actually execute to
    /// prove the SQL Server exit criteria (docs/09-TEST-STRATEGY.md §2); on a developer
    /// workstation the same situation only skips.
    /// </summary>
    public static bool RunningInContinuousIntegration =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds a connection string for one of the hard-coded disposable databases. Any
    /// catalog in <c>Faed_TEST_CONNECTION</c> is deliberately replaced, so an application
    /// or production database cannot be selected through configuration.
    /// </summary>
    public static string ConnectionStringFor(string databaseName)
    {
        if (!AllowedDatabaseNames.Contains(databaseName))
        {
            throw new ArgumentException(
                $"'{databaseName}' is not an allowed disposable Faed test database.",
                nameof(databaseName));
        }

        var configured = Environment.GetEnvironmentVariable(BaseConnectionEnvVar);
        var baseConnection = string.IsNullOrWhiteSpace(configured) ? DefaultLocalDbConnection : configured;

        var connectionString = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;

        AssertSafeTestDatabase(connectionString, databaseName);
        return connectionString;
    }

    /// <summary>Defense-in-depth guard to call immediately before create/drop operations.</summary>
    public static void AssertSafeTestDatabase(string connectionString, string expectedDatabaseName)
    {
        if (!AllowedDatabaseNames.Contains(expectedDatabaseName))
        {
            throw new InvalidOperationException(
                $"'{expectedDatabaseName}' is not an allowed disposable Faed test database.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Refusing destructive database operation against an attached database file.");
        }

        var actualDatabaseName = builder.InitialCatalog;
        if (!string.Equals(actualDatabaseName, expectedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing destructive database operation: expected '{expectedDatabaseName}', " +
                $"but the connection targets '{actualDatabaseName}'.");
        }
    }

    public static async Task<bool> IsReachableAsync(string connectionString)
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
