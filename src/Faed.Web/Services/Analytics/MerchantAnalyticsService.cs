using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Analytics;

/// <inheritdoc />
public sealed class MerchantAnalyticsService(
    IApplicationDbContext db,
    IClock clock,
    IOptions<AnalyticsOptions> options) : IMerchantAnalyticsService
{
    private readonly AnalyticsOptions _options = options.Value;

    public async Task<MerchantAnalyticsView> GetForOwnerAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == merchantUserId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (merchantId is null)
        {
            return new MerchantAnalyticsView(
                0m, 0m, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, [], _options.StaleListingThreshold);
        }

        var mid = merchantId.Value;
        var nowUtc = clock.UtcNow;

        var completedOrders = db.Orders
            .AsNoTracking()
            .Where(o => o.MerchantProfileId == mid && o.Status == OrderStatus.Completed);

        var completedDeals = db.B2BDeals
            .AsNoTracking()
            .Where(d => d.SellingMerchantProfileId == mid && d.Status == B2BDealStatus.Completed);

        // ---- Recovered value: completed transactions only, from immutable line snapshots ----
        // (docs/03-BUSINESS-RULES.md §15 "Derive from completed orders / completed deals").
        // The delivery-fee snapshot is deliberately excluded: recovered value is the value
        // recovered *from inventory*, not fulfilment charges (docs/15-GLOSSARY.md "Recovered Value").
        var recoveredB2C = await completedOrders
            .SelectMany(o => o.Items)
            .SumAsync(i => (decimal?)i.LineTotalSnapshot, cancellationToken) ?? 0m;

        var recoveredB2B = await completedDeals
            .SelectMany(d => d.Lines)
            .SumAsync(l => (decimal?)l.LineTotalSnapshot, cancellationToken) ?? 0m;

        var unitsSoldB2C = await completedOrders
            .SelectMany(o => o.Items)
            .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;

        var unitsSoldB2B = await completedDeals
            .SelectMany(d => d.Lines)
            .SumAsync(l => (int?)l.Quantity, cancellationToken) ?? 0;

        // ---- Units listed: introduced supply = opening balance + every positive adjustment ----
        // Negative adjustments explain units removed from sale; they do not undo the fact that
        // those units were introduced (docs/03-BUSINESS-RULES.md section 5 invariant).
        var initialUnits = await db.Listings
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == mid)
            .SelectMany(l => l.Variants)
            .SumAsync(v => (int?)v.InitialQuantity, cancellationToken) ?? 0;

        var positivelyAdjustedUnits = await (
            from adjustment in db.InventoryAdjustments.AsNoTracking()
            join variant in db.ListingVariants.AsNoTracking()
                on adjustment.ListingVariantId equals variant.Id
            join listing in db.Listings.AsNoTracking()
                on variant.ListingId equals listing.Id
            where listing.MerchantProfileId == mid && adjustment.QuantityDelta > 0
            select (int?)adjustment.QuantityDelta)
            .SumAsync(cancellationToken) ?? 0;

        var unitsListed = checked(initialUnits + positivelyAdjustedUnits);

        // ---- Order / deal volume and cancellations ----
        var orderCountsByStatus = await db.Orders
            .AsNoTracking()
            .Where(o => o.MerchantProfileId == mid)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var completedOrderCount = orderCountsByStatus.Where(x => x.Status == OrderStatus.Completed).Sum(x => x.Count);
        var cancelledOrders = orderCountsByStatus.Where(x => x.Status == OrderStatus.Cancelled).Sum(x => x.Count);
        var noShowOrders = orderCountsByStatus.Where(x => x.Status == OrderStatus.NoShow).Sum(x => x.Count);

        var dealCountsByStatus = await db.B2BDeals
            .AsNoTracking()
            .Where(d => d.SellingMerchantProfileId == mid)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var completedDealCount = dealCountsByStatus.Where(x => x.Status == B2BDealStatus.Completed).Sum(x => x.Count);
        var cancelledDeals = dealCountsByStatus.Where(x => x.Status == B2BDealStatus.Cancelled).Sum(x => x.Count);

        var activeNegotiations = await db.B2BNegotiations
            .AsNoTracking()
            .CountAsync(
                n => n.Status == B2BNegotiationStatus.Open
                    && n.Revisions.Any(r =>
                        r.RevisionNumber == n.CurrentRevisionNumber
                        && r.OfferExpiresAtUtc > nowUtc)
                    && (n.SellingMerchantProfileId == mid || n.BuyingMerchantProfileId == mid),
                cancellationToken);

        // ---- Average time to sale: listing publication -> completed sale, per sold unit ----
        // Order/deal creation-to-completion measures fulfillment duration, not how long the
        // merchant's inventory took to sell. Each line is weighted by quantity so twelve units
        // sold after four days contribute twelve inventory observations, not one.
        var orderSaleDurations = await (
            from item in db.OrderItems.AsNoTracking()
            join order in db.Orders.AsNoTracking() on item.OrderId equals order.Id
            join listing in db.Listings.AsNoTracking() on item.ListingId equals listing.Id
            where order.MerchantProfileId == mid
                && order.Status == OrderStatus.Completed
                && order.CompletedAtUtc != null
                && listing.PublishedAtUtc != null
                && listing.PublishedAtUtc <= order.CompletedAtUtc
            select new
            {
                Minutes = EF.Functions.DateDiffMinute(listing.PublishedAtUtc!.Value, order.CompletedAtUtc!.Value),
                item.Quantity,
            })
            .ToListAsync(cancellationToken);

        var dealSaleDurations = await (
            from line in db.B2BDealLines.AsNoTracking()
            join deal in db.B2BDeals.AsNoTracking() on line.B2BDealId equals deal.Id
            join variant in db.ListingVariants.AsNoTracking() on line.ListingVariantId equals variant.Id
            join listing in db.Listings.AsNoTracking() on variant.ListingId equals listing.Id
            where deal.SellingMerchantProfileId == mid
                && deal.Status == B2BDealStatus.Completed
                && deal.CompletedAtUtc != null
                && listing.PublishedAtUtc != null
                && listing.PublishedAtUtc <= deal.CompletedAtUtc
            select new
            {
                Minutes = EF.Functions.DateDiffMinute(listing.PublishedAtUtc!.Value, deal.CompletedAtUtc!.Value),
                line.Quantity,
            })
            .ToListAsync(cancellationToken);

        var soldUnitCount = orderSaleDurations.Sum(x => x.Quantity) + dealSaleDurations.Sum(x => x.Quantity);
        var weightedMinutes = orderSaleDurations.Sum(x => (long)x.Minutes * x.Quantity)
            + dealSaleDurations.Sum(x => (long)x.Minutes * x.Quantity);
        double? averageDaysToSale = soldUnitCount == 0
            ? null
            : weightedMinutes / (double)soldUnitCount / 1_440d;

        // ---- Stale listings: published past the threshold, never sold a unit ----
        var staleCutoff = _options.StaleListingThreshold >= nowUtc - DateTime.MinValue
            ? DateTime.MinValue
            : nowUtc - _options.StaleListingThreshold;
        var staleCandidates = await db.Listings
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == mid
                && l.Status == ListingStatus.Live
                && l.PublishedAtUtc != null
                && l.PublishedAtUtc < staleCutoff
                && !l.Variants.Any(v => v.SoldQuantity > 0))
            .Select(l => new
            {
                l.Id,
                l.Title,
                l.Slug,
                PublishedAtUtc = l.PublishedAtUtc!.Value,
                AvailableUnits = l.Variants.Where(v => v.IsActive).Sum(v => (int?)v.AvailableQuantity) ?? 0,
            })
            .OrderBy(l => l.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        var staleListings = staleCandidates
            .Select(l => new StaleListingView(
                l.Id, l.Title, l.Slug, l.PublishedAtUtc, l.AvailableUnits,
                Math.Max(0, (int)(nowUtc - l.PublishedAtUtc).TotalDays)))
            .ToList();

        return new MerchantAnalyticsView(
            recoveredB2C,
            recoveredB2B,
            unitsListed,
            unitsSoldB2C,
            unitsSoldB2B,
            completedOrderCount,
            completedDealCount,
            averageDaysToSale,
            cancelledOrders,
            noShowOrders,
            cancelledDeals,
            activeNegotiations,
            staleListings,
            _options.StaleListingThreshold);
    }
}
