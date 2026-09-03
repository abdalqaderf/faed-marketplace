using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Analytics;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Faed.IntegrationTests;

/// <summary>
/// Merchant recovery analytics against real SQL Server (tasks/TASK-010-ANALYTICS-AND-ADMIN.md
/// "Analytics reconcile with known seeded completed transactions"; docs/03-BUSINESS-RULES.md
/// §15 — derived from completed orders / deals, never a stored total).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class MerchantAnalyticsServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task Analytics_ReconcileWithTheMerchantsCompletedOrdersAndDeals()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);

        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        // One completed B2C order: 1 unit @ JOD 20.000 (see TrustScope.CreateLiveB2CListingAsync).
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        await scope.CompleteOrderAsync(sellerUserId, orderId);

        // One completed B2B deal: 12 units @ JOD 4.000 = JOD 48.000.
        await scope.CreateCompletedDealAsync(sellerUserId, buyerMerchantUserId);

        // One cancelled B2C order and one still-open negotiation for the same seller.
        await scope.CreateCancelledOrderAsync(sellerUserId);
        await scope.StartOpenNegotiationAsync(sellerUserId, buyerMerchantUserId);

        var a = await scope.Analytics.GetForOwnerAsync(sellerUserId);

        Assert.Equal(20.000m, a.RecoveredValueB2C);
        Assert.Equal(48.000m, a.RecoveredValueB2B);
        Assert.Equal(68.000m, a.RecoveredValueTotal);
        Assert.Equal(1, a.CompletedOrders);
        Assert.Equal(1, a.CompletedDeals);
        Assert.Equal(1, a.UnitsSoldB2C);
        Assert.Equal(12, a.UnitsSoldB2B);
        Assert.Equal(13, a.UnitsSoldTotal);
        Assert.Equal(1, a.CancelledOrders);
        Assert.Equal(1, a.ActiveNegotiations);
        Assert.NotNull(a.AverageDaysToSale);
        Assert.True(a.AverageDaysToSale >= 0);

        // Sell-through is units sold over units listed, both server-derived.
        Assert.True(a.UnitsListed >= a.UnitsSoldTotal);
        Assert.Equal((double)a.UnitsSoldTotal / a.UnitsListed, a.SellThroughRate, 5);
    }

    [SkippableFact]
    public async Task UnitsListed_IncludesPositiveAdjustments_AndSellThroughUsesIntroducedSupply()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        var variantId = await scope.Db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.ListingVariantId)
            .SingleAsync();

        var inventory = scope.Services.GetRequiredService<IInventoryService>();
        var replenished = await inventory.AdjustStockAsync(sellerUserId, new StockAdjustmentInput(
            variantId, InventoryAdjustmentType.StockFound, 5, "Found another carton."));
        Assert.True(replenished.Succeeded, replenished.Error);
        var reduced = await inventory.AdjustStockAsync(sellerUserId, new StockAdjustmentInput(
            variantId, InventoryAdjustmentType.StockLostOrDamaged, -2, "Two units were damaged."));
        Assert.True(reduced.Succeeded, reduced.Error);
        await scope.CompleteOrderAsync(sellerUserId, orderId);

        var analytics = await scope.Analytics.GetForOwnerAsync(sellerUserId);

        Assert.Equal(15, analytics.UnitsListed);
        Assert.Equal(1, analytics.UnitsSoldTotal);
        Assert.Equal(1d / 15d, analytics.SellThroughRate, 8);
    }

    [SkippableFact]
    public async Task AverageTimeToSale_UsesListingPublicationAndIsWeightedBySoldUnits()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        var (_, orderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        await scope.CompleteOrderAsync(sellerUserId, orderId);
        var dealId = await scope.CreateCompletedDealAsync(sellerUserId, buyerMerchantUserId);

        var orderListingId = await scope.Db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.ListingId)
            .SingleAsync();
        var dealListingId = await (
            from line in scope.Db.B2BDealLines.AsNoTracking()
            join variant in scope.Db.ListingVariants.AsNoTracking() on line.ListingVariantId equals variant.Id
            where line.B2BDealId == dealId
            select variant.ListingId)
            .SingleAsync();

        var completedAtUtc = DateTime.UtcNow;
        await scope.Db.Orders.Where(o => o.Id == orderId).ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.CompletedAtUtc, completedAtUtc));
        await scope.Db.B2BDeals.Where(d => d.Id == dealId).ExecuteUpdateAsync(setters => setters
            .SetProperty(d => d.CompletedAtUtc, completedAtUtc));
        await scope.Db.Listings.Where(l => l.Id == orderListingId).ExecuteUpdateAsync(setters => setters
            .SetProperty(l => l.PublishedAtUtc, completedAtUtc.AddDays(-10)));
        await scope.Db.Listings.Where(l => l.Id == dealListingId).ExecuteUpdateAsync(setters => setters
            .SetProperty(l => l.PublishedAtUtc, completedAtUtc.AddDays(-4)));

        var analytics = await scope.Analytics.GetForOwnerAsync(sellerUserId);

        var expectedDays = (10d + (12d * 4d)) / 13d;
        Assert.Equal(expectedDays, analytics.AverageDaysToSale!.Value, 8);
    }

    [SkippableFact]
    public async Task ActiveNegotiations_ExcludeAnExpiredCurrentRevisionBeforeTheSweepRuns()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var negotiationId = await scope.StartOpenNegotiationAsync(sellerUserId, buyerMerchantUserId);

        await scope.Db.B2BOfferRevisions
            .Where(r => r.B2BNegotiationId == negotiationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.OfferExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        Assert.Equal(
            B2BNegotiationStatus.Open,
            await scope.Db.B2BNegotiations.AsNoTracking()
                .Where(n => n.Id == negotiationId)
                .Select(n => n.Status)
                .SingleAsync());
        Assert.Equal(0, (await scope.Analytics.GetForOwnerAsync(sellerUserId)).ActiveNegotiations);
    }

    [SkippableFact]
    public async Task Analytics_ForAUserWithoutAMerchantProfile_AreAllZero()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var strangerUserId = await scope.CreateUserAsync();

        var a = await scope.Analytics.GetForOwnerAsync(strangerUserId);

        Assert.False(a.HasAnyActivity);
        Assert.Equal(0m, a.RecoveredValueTotal);
        Assert.Empty(a.StaleListings);
    }

    [SkippableFact]
    public async Task StaleListings_UseTheExactConfiguredDurationAndStrictOlderThanBoundary()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (_, orderId) = await scope.CreateCancelledOrderAsync(sellerUserId);
        var listingId = await scope.Db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.ListingId)
            .SingleAsync();
        var nowUtc = DateTime.UtcNow;
        var threshold = TimeSpan.FromHours(36.5);
        await scope.Db.Listings.Where(l => l.Id == listingId).ExecuteUpdateAsync(setters => setters
            .SetProperty(l => l.PublishedAtUtc, nowUtc - threshold));

        var analytics = new MerchantAnalyticsService(
            scope.Db,
            new FixedClock(nowUtc),
            Options.Create(new AnalyticsOptions { StaleListingThreshold = threshold }));

        var exactlyAtBoundary = await analytics.GetForOwnerAsync(sellerUserId);
        Assert.Empty(exactlyAtBoundary.StaleListings);
        Assert.Equal(threshold, exactlyAtBoundary.StaleListingThreshold);
        Assert.Equal("1 day 12 hours 30 minutes", exactlyAtBoundary.StaleListingThresholdLabel);

        await scope.Db.Listings.Where(l => l.Id == listingId).ExecuteUpdateAsync(setters => setters
            .SetProperty(l => l.PublishedAtUtc, nowUtc - threshold - TimeSpan.FromSeconds(1)));

        Assert.Single((await analytics.GetForOwnerAsync(sellerUserId)).StaleListings);
    }

    [SkippableFact]
    public async Task StaleListings_AreThePublishedNeverSoldListingsPastTheThreshold()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();

        // A Live listing that never sold (an order was placed then cancelled).
        await scope.CreateCancelledOrderAsync(sellerUserId);
        // A Live listing that did sell — must NOT be flagged stale.
        var (_, soldOrderId) = await scope.CreateConfirmedOrderAsync(sellerUserId);
        await scope.CompleteOrderAsync(sellerUserId, soldOrderId);

        // Force every published listing past the "stale" threshold.
        var analytics = new MerchantAnalyticsService(
            scope.Db,
            scope.Services.GetRequiredService<IClock>(),
            Options.Create(new AnalyticsOptions { StaleListingThreshold = TimeSpan.FromTicks(1) }));

        var a = await analytics.GetForOwnerAsync(sellerUserId);

        Assert.Single(a.StaleListings);
        Assert.All(a.StaleListings, s => Assert.True(s.AgeDays >= 0));
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
