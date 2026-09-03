using Faed.Web.Services.Analytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Faed.UnitTests;

public sealed class AnalyticsOptionsTests
{
    private readonly AnalyticsOptionsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StaleListingThreshold_mustBePositive(int ticks)
    {
        var result = _validator.Validate(
            null,
            new AnalyticsOptions { StaleListingThreshold = TimeSpan.FromTicks(ticks) });

        Assert.True(result.Failed);
        Assert.Contains("must be a positive duration", result.FailureMessage);
    }

    [Fact]
    public void PositiveStaleListingThreshold_isValid()
    {
        var result = _validator.Validate(
            null,
            new AnalyticsOptions { StaleListingThreshold = TimeSpan.FromHours(1) });

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    [Fact]
    public void MalformedStaleListingThreshold_doesNotSilentlyFallBackToTheDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analytics:StaleListingThreshold"] = "not-a-duration",
            })
            .Build();
        var options = new AnalyticsOptions();

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetSection(AnalyticsOptions.SectionName).Bind(options));
    }
}
