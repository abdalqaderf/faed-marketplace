namespace Faed.IntegrationTests.Support;

/// <summary>
/// A hosted Faed.Web bound to its own disposable catalog
/// (<see cref="TestSqlServer.DemoSeedDatabaseName"/>). The demo-seed test drives ~40 real
/// service calls end to end; giving it its own database keeps it clear of the connection
/// state and locks the ~180 tests on the shared <see cref="TestSqlServer.WebDatabaseName"/>
/// catalog leave behind, and keeps its large fixed data set from polluting them.
/// </summary>
public sealed class DemoSeedWebApplicationFactory : FaedWebApplicationFactory
{
    protected override string DatabaseName => TestSqlServer.DemoSeedDatabaseName;
}

[CollectionDefinition(Name)]
public sealed class DemoSeedWebCollection : ICollectionFixture<DemoSeedWebApplicationFactory>
{
    public const string Name = "faed-demo-seed";
}
