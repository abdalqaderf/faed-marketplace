using Faed.Web;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Faed.UnitTests;

/// <summary>
/// Private-file storage is a Development-only convenience (tasks/TASK-011-HARDENING-AND-DEMO.md
/// finding 3, docs/08-SECURITY-AND-PRIVACY.md §3): every non-Development environment must
/// register a real protected object store, and gets a fail-loud stub until it does.
/// </summary>
public sealed class PrivateFileStorageRegistrationTests
{
    private static IServiceProvider Build(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddFaedPlatform(new ConfigurationBuilder().Build(), new FakeHostEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Development_RegistersLocalFileStorage()
    {
        var storage = Build("Development").GetRequiredService<IFileStorage>();
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void NonDevelopment_GetsAStubThatFailsUntilARealStoreIsRegistered(string environmentName)
    {
        var provider = Build(environmentName);
        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IFileStorage>());
        Assert.Contains(environmentName, ex.Message);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Faed.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
