using Faed.Web.Data;
using Faed.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Guards the rule that the hosted integration suite writes only to a disposable,
/// allow-listed test catalog and never to the application database
/// (docs/09-TEST-STRATEGY.md §2). This regressed once already: the composition root read
/// <c>ConnectionStrings:DefaultConnection</c> eagerly, so the test host's override was
/// ignored and every web test wrote to the application catalog.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class TestHostDatabaseTargetTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public void HostedApplicationDbContext_TargetsTheDisposableTestCatalog()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var catalog = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;

        Assert.Equal(TestSqlServer.WebDatabaseName, catalog);
    }

    [SkippableFact]
    public void HostedIdentityStore_TargetsTheSameDisposableTestCatalog()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();

        // Identity shares the one application DbContext (docs/06-ARCHITECTURE.md §5), so a
        // single assertion above could still miss a second, differently configured context.
        var contexts = scope.ServiceProvider.GetServices<ApplicationDbContext>().ToList();

        Assert.NotEmpty(contexts);
        Assert.All(contexts, db => Assert.Equal(
            TestSqlServer.WebDatabaseName,
            new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog));
    }
}
