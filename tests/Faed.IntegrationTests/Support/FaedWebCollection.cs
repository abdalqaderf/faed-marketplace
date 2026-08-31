namespace Faed.IntegrationTests.Support;

/// <summary>
/// Shares a single hosted app + test database across the web integration test classes so
/// the disposable database is created and dropped exactly once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class FaedWebCollection : ICollectionFixture<FaedWebApplicationFactory>
{
    public const string Name = "faed-web";
}
