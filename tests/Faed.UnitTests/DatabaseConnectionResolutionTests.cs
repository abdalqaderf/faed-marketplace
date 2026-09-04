using Faed.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Faed.UnitTests;

/// <summary>
/// Fail-fast database configuration (tasks/TASK-011-HARDENING-AND-DEMO.md finding 2): a
/// non-Development environment must never silently fall back to the committed local
/// development database, and must fail when no connection string is configured.
/// </summary>
public sealed class DatabaseConnectionResolutionTests
{
    private const string LocalDb =
        "Server=(localdb)\\MSSQLLocalDB;Database=Faed;Trusted_Connection=True;MultipleActiveResultSets=true";
    private const string RealServer =
        "Server=db.internal;Database=Faed;User Id=faed_app;Password=x;TrustServerCertificate=true";

    private static IConfiguration Config(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

    private static IHostEnvironment Environment(string name) => new FakeHostEnvironment(name);

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Faed.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Development_UsesTheConfiguredLocalDbConnection()
    {
        var resolved = DependencyInjection.ResolveDatabaseConnectionString(
            Config(LocalDb), Environment("Development"));

        Assert.Equal(LocalDb, resolved);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public void NonDevelopment_WithNoConnectionString_ThrowsAtStartup(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.ResolveDatabaseConnectionString(Config(null), Environment(environmentName)));

        Assert.Contains(environmentName, ex.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NonDevelopment_StillPointedAtLocalDb_ThrowsAtStartup(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.ResolveDatabaseConnectionString(Config(LocalDb), Environment(environmentName)));

        Assert.Contains("local development database", ex.Message);
    }

    [Fact]
    public void Production_WithARealServer_IsAccepted()
    {
        var resolved = DependencyInjection.ResolveDatabaseConnectionString(
            Config(RealServer), Environment("Production"));

        Assert.Equal(RealServer, resolved);
    }

    [Fact]
    public void Testing_IsExempt_SoTheIntegrationHostCanUseItsDisposableLocalDbCatalog()
    {
        var resolved = DependencyInjection.ResolveDatabaseConnectionString(
            Config(LocalDb), Environment("Testing"));

        Assert.Equal(LocalDb, resolved);
    }
}
