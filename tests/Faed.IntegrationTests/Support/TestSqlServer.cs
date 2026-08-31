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

    private const string DefaultLocalDbConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=Faed_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true";

    public static bool WasExplicitlyConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BaseConnectionEnvVar));

    /// <summary>A connection string for <paramref name="databaseName"/> (must read as a test DB).</summary>
    public static string ConnectionStringFor(string databaseName)
    {
        if (!databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Test database names must contain 'test'.", nameof(databaseName));
        }

        var configured = Environment.GetEnvironmentVariable(BaseConnectionEnvVar);
        var baseConnection = string.IsNullOrWhiteSpace(configured) ? DefaultLocalDbConnection : configured;

        return new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;
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
