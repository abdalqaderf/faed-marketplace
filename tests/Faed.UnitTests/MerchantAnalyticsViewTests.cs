using Faed.Web.Services.Analytics;

namespace Faed.UnitTests;

/// <summary>
/// The derived figures on <see cref="MerchantAnalyticsView"/> (tasks/TASK-010-ANALYTICS-AND-ADMIN.md).
/// The service computes the raw counts from the database; these are the pure roll-ups the
/// view renders.
/// </summary>
public sealed class MerchantAnalyticsViewTests
{
    private static MerchantAnalyticsView View(
        decimal b2c = 0, decimal b2b = 0, int listed = 0, int soldB2C = 0, int soldB2B = 0,
        int completedOrders = 0, int completedDeals = 0, double? avgDays = null,
        int cancelledOrders = 0, int noShowOrders = 0, int cancelledDeals = 0, int activeNegotiations = 0) =>
        new(b2c, b2b, listed, soldB2C, soldB2B, completedOrders, completedDeals, avgDays,
            cancelledOrders, noShowOrders, cancelledDeals, activeNegotiations, [], TimeSpan.FromDays(30));

    [Fact]
    public void RecoveredValueTotal_isTheSumOfBothChannels()
    {
        var v = View(b2c: 120.500m, b2b: 480.000m);
        Assert.Equal(600.500m, v.RecoveredValueTotal);
    }

    [Fact]
    public void UnitsSoldTotal_isTheSumOfBothChannels()
    {
        var v = View(soldB2C: 3, soldB2B: 12);
        Assert.Equal(15, v.UnitsSoldTotal);
    }

    [Theory]
    [InlineData(0, 0, 0d)]
    [InlineData(0, 10, 0d)]
    [InlineData(5, 20, 0.25d)]
    [InlineData(20, 20, 1d)]
    public void SellThroughRate_isSoldOverListed_andZeroWhenNothingIsListed(int soldB2C, int listed, double expected)
    {
        var v = View(listed: listed, soldB2C: soldB2C);
        Assert.Equal(expected, v.SellThroughRate, 5);
    }

    [Fact]
    public void HasAnyActivity_isFalseForABrandNewMerchant()
    {
        Assert.False(View().HasAnyActivity);
    }

    [Fact]
    public void HasAnyActivity_isTrueOnceThereIsListedStockOrAnyTransaction()
    {
        Assert.True(View(listed: 4).HasAnyActivity);
        Assert.True(View(completedDeals: 1).HasAnyActivity);
        Assert.True(View(cancelledOrders: 1).HasAnyActivity);
        Assert.True(View(activeNegotiations: 1).HasAnyActivity);
    }

    [Fact]
    public void StaleThresholdLabel_preservesTheConfiguredDurationWithoutRoundingToDays()
    {
        var view = View() with { StaleListingThreshold = TimeSpan.FromHours(36.5) };

        Assert.Equal(TimeSpan.FromHours(36.5), view.StaleListingThreshold);
        Assert.Equal("1 day 12 hours 30 minutes", view.StaleListingThresholdLabel);
    }
}
