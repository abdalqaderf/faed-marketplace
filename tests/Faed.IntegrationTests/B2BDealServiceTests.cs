using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Ordering;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.IntegrationTests;

/// <summary>
/// Accepted B2B deal and fulfilment against real SQL Server (tasks/TASK-008-B2B-DEALS.md
/// "Mandatory tests" / "Exit criteria"; docs/09-TEST-STRATEGY.md §3 "B2B deal" — reservation
/// concurrency is proven against SQL Server, never InMemory/SQLite).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class B2BDealServiceTests(FaedWebApplicationFactory factory)
{
    private static readonly AcceptOfferInput PickupAccept = new(B2BFulfillmentType.Pickup);

    [SkippableFact]
    public async Task AcceptOffer_ReservesEveryLineAtomically_AndCreatesTheDeal()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12), new B2BOfferLineInput(variantIds[1], 15)], 4.000m, null, null));
        Assert.True(start.Succeeded, start.Error);

        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        var variantA = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        var variantB = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[1]);
        Assert.Equal((18, 12), (variantA.AvailableQuantity, variantA.ReservedQuantity));
        Assert.Equal((15, 15), (variantB.AvailableQuantity, variantB.ReservedQuantity));

        var deal = await scope.Db.B2BDeals.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.Id == accept.Value);
        Assert.Equal(B2BDealStatus.AwaitingFulfillment, deal.Status);
        Assert.Equal(27, deal.TotalUnits);
        Assert.Equal(108.000m, deal.SubtotalSnapshot);
        Assert.Equal(108.000m, deal.TotalSnapshot);
        Assert.Equal(2, deal.Lines.Count);
    }

    [SkippableFact]
    public async Task AcceptOffer_WhenOneLineCannotReserve_ReservesNothing_AndLeavesTheNegotiationOpen()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        // variantIds[0] has 12 in stock, variantIds[1] has 12; the offer asks for 12 + 20.
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 12);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12), new B2BOfferLineInput(variantIds[1], 20)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);

        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Failed);
        Assert.Equal(ResultErrorKind.Conflict, accept.ErrorKind);

        var variantA = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((12, 0), (variantA.AvailableQuantity, variantA.ReservedQuantity));
        var negotiationStatus = await scope.Db.B2BNegotiations.AsNoTracking()
            .Where(n => n.Id == start.Value).Select(n => n.Status).SingleAsync();
        Assert.Equal(B2BNegotiationStatus.Open, negotiationStatus);
        Assert.Empty(await scope.Db.B2BDeals.AsNoTracking().Where(d => d.B2BNegotiationId == start.Value).ToListAsync());
    }

    [SkippableFact]
    public async Task TwoB2BAcceptances_CompetingForTheSameStock_CannotOversell()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyer1, _) = await scope.CreateApprovedMerchantAsync();
        var (buyer2, _) = await scope.CreateApprovedMerchantAsync();
        // Exactly enough stock for one 10-unit lot, not two.
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 10);

        var n1 = await scope.Negotiations.StartNegotiationAsync(buyer1, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var n2 = await scope.Negotiations.StartNegotiationAsync(buyer2, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        Assert.True(n1.Succeeded && n2.Succeeded);

        await using var scope2 = new DealScope(factory);
        Result<Guid> result2 = null!;
        var service1 = scope.NewGatedDealService(async ct =>
            result2 = await scope2.Deals.AcceptOfferAsync(sellerUserId, n2.Value, PickupAccept, ct));

        var result1 = await service1.AcceptOfferAsync(sellerUserId, n1.Value, PickupAccept);

        Assert.True(result2.Succeeded, result2.Error);
        Assert.True(result1.Failed);
        Assert.Equal(ResultErrorKind.Conflict, result1.ErrorKind);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((0, 10), (variant.AvailableQuantity, variant.ReservedQuantity));
        Assert.Equal(1, await scope.Db.B2BDeals.AsNoTracking()
            .CountAsync(d => d.B2BNegotiationId == n1.Value || d.B2BNegotiationId == n2.Value));
    }

    [SkippableFact]
    public async Task AB2COrderAndAB2BAcceptance_CompetingForTheLastUnits_AreSafe()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchant, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(sellerUserId);
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 10);
        var consumerId = await scope.CreateUserAsync();

        var negotiation = await scope.Negotiations.StartNegotiationAsync(buyerMerchant, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        Assert.True(negotiation.Succeeded, negotiation.Error);

        await using var orderScope = new DealScope(factory);
        Result<Guid> b2cResult = null!;
        var gatedDeal = scope.NewGatedDealService(async ct =>
            b2cResult = await orderScope.Orders.PlaceOrderAsync(consumerId, new PlaceOrderInput(
                [new OrderLineInput(variantIds[0], 2)],
                OrderFulfillmentType.Pickup, locationId, null, null, "Consumer", "079", null), ct));

        var b2bResult = await gatedDeal.AcceptOfferAsync(sellerUserId, negotiation.Value, PickupAccept);

        // The B2C order slipped in and took 2 units; the B2B acceptance for 10 can no longer
        // reserve and loses the race cleanly (no oversell, no deal).
        Assert.True(b2cResult.Succeeded, b2cResult.Error);
        Assert.True(b2bResult.Failed);
        Assert.Equal(ResultErrorKind.Conflict, b2bResult.ErrorKind);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((8, 2, 0), (variant.AvailableQuantity, variant.ReservedQuantity, variant.SoldQuantity));
        Assert.Empty(await scope.Db.B2BDeals.AsNoTracking().Where(d => d.B2BNegotiationId == negotiation.Value).ToListAsync());
    }

    [SkippableFact]
    public async Task ExpiredDealReservation_IsReleasedByTheSweep_ExactlyOnce()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        await scope.Db.B2BDeals.Where(d => d.Id == accept.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(-5)));

        var first = await scope.Deals.ReleaseExpiredDealReservationsAsync();
        var second = await scope.Deals.ReleaseExpiredDealReservationsAsync();
        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((30, 0, 0), (variant.AvailableQuantity, variant.ReservedQuantity, variant.SoldQuantity));
        var status = await scope.Db.B2BDeals.AsNoTracking().Where(d => d.Id == accept.Value).Select(d => d.Status).SingleAsync();
        Assert.Equal(B2BDealStatus.Cancelled, status);
    }

    [SkippableFact]
    public async Task Completion_MovesReservedStockToSold()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);
        var dealId = accept.Value;

        Assert.True((await scope.Deals.MarkReadyForPickupAsync(sellerUserId, dealId)).Succeeded);
        Assert.True((await scope.Deals.MarkDeliveredAsync(sellerUserId, dealId)).Succeeded);
        var complete = await scope.Deals.CompleteAsync(buyerUserId, dealId);
        Assert.True(complete.Succeeded, complete.Error);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((18, 0, 12), (variant.AvailableQuantity, variant.ReservedQuantity, variant.SoldQuantity));
    }

    [SkippableFact]
    public async Task CancellingAnAwaitingDeal_ReleasesTheReservedStock()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        var cancel = await scope.Deals.CancelAsync(buyerUserId, accept.Value, "changed our plan");
        Assert.True(cancel.Succeeded, cancel.Error);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((30, 0, 0), (variant.AvailableQuantity, variant.ReservedQuantity, variant.SoldQuantity));
    }

    [SkippableFact]
    public async Task SellerArrangedShipping_StoresTheShipmentReference()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(
            sellerUserId, start.Value, new AcceptOfferInput(B2BFulfillmentType.SellerArrangedShipping));
        Assert.True(accept.Succeeded, accept.Error);

        var deal = await scope.Db.B2BDeals.AsNoTracking().SingleAsync(d => d.Id == accept.Value);
        // No shipping charge is agreed at acceptance; the total is the agreed subtotal alone.
        Assert.Equal(48.000m, deal.TotalSnapshot);
        Assert.Null(deal.ShippingCostSnapshot);
        Assert.Null(deal.ShipmentReference);

        Assert.True((await scope.Deals.MarkShippedAsync(sellerUserId, accept.Value, "WB-8842")).Succeeded);
        var reference = await scope.Db.B2BDeals.AsNoTracking()
            .Where(d => d.Id == accept.Value).Select(d => d.ShipmentReference).SingleAsync();
        Assert.Equal("WB-8842", reference);

        // The buying merchant cannot drive the seller's fulfilment steps.
        Assert.Equal(ResultErrorKind.Forbidden,
            (await scope.Deals.SetShipmentReferenceAsync(buyerUserId, accept.Value, "nope")).ErrorKind);
    }

    [SkippableFact]
    public async Task ADealIsInvisibleAndUntouchableByAMerchantThatIsNotAParticipant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (strangerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        Assert.NotNull(await scope.Deals.GetDealAsync(sellerUserId, accept.Value));
        Assert.NotNull(await scope.Deals.GetDealAsync(buyerUserId, accept.Value));
        Assert.Null(await scope.Deals.GetDealAsync(strangerUserId, accept.Value));

        var act = await scope.Deals.CancelAsync(strangerUserId, accept.Value, "not mine");
        Assert.Equal(ResultErrorKind.NotFound, act.ErrorKind);
        Assert.Empty(await scope.Deals.GetMyDealsAsync(strangerUserId, B2BDealFilter.All));
    }

    // ---- Post-review regressions (Codex review — TASK-008) ---------------------

    [SkippableFact]
    public async Task AcceptOffer_WhenTheSellingMerchantHasBeenSuspended_IsRejected_AndReservesNothing()
    {
        // Finding 1: both merchants must still be approved when the deal is created. A seller
        // suspended after the negotiation opened cannot be a party to a new deal
        // (docs/03-BUSINESS-RULES.md §1).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, sellerProfileId) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);

        await scope.SuspendMerchantAsync(sellerProfileId);

        var accept = await scope.Deals.AcceptOfferAsync(buyerUserId, start.Value, PickupAccept);
        Assert.True(accept.Failed);
        Assert.Equal(ResultErrorKind.Conflict, accept.ErrorKind);

        Assert.Empty(await scope.Db.B2BDeals.AsNoTracking().Where(d => d.B2BNegotiationId == start.Value).ToListAsync());
        var negotiationStatus = await scope.Db.B2BNegotiations.AsNoTracking()
            .Where(n => n.Id == start.Value).Select(n => n.Status).SingleAsync();
        Assert.Equal(B2BNegotiationStatus.Open, negotiationStatus);
        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((30, 0), (variant.AvailableQuantity, variant.ReservedQuantity));
    }

    [SkippableFact]
    public async Task AcceptOffer_WhenTheBuyingMerchantHasBeenSuspended_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, buyerProfileId) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);

        await scope.SuspendMerchantAsync(buyerProfileId);

        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Failed);
        Assert.Equal(ResultErrorKind.Conflict, accept.ErrorKind);
        Assert.Empty(await scope.Db.B2BDeals.AsNoTracking().Where(d => d.B2BNegotiationId == start.Value).ToListAsync());
    }

    [SkippableFact]
    public async Task AdvancingAnExpiredDealToFulfillment_IsRejected_AndTheSweepThenReleasesTheStockExactlyOnce()
    {
        // Finding 2: an expired reservation must not be advanced to a fulfilment state (which
        // would clear the window and hold stock forever). The transition is refused
        // synchronously; the sweep then releases the stock exactly once.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        await scope.Db.B2BDeals.Where(d => d.Id == accept.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(-5)));

        await using (var actionScope = new DealScope(factory))
        {
            var ready = await actionScope.Deals.MarkReadyForPickupAsync(sellerUserId, accept.Value);
            Assert.True(ready.Failed);
            Assert.Equal(ResultErrorKind.Conflict, ready.ErrorKind);
        }

        var midDeal = await scope.Db.B2BDeals.AsNoTracking().SingleAsync(d => d.Id == accept.Value);
        Assert.Equal(B2BDealStatus.AwaitingFulfillment, midDeal.Status);
        var midVariant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((18, 12), (midVariant.AvailableQuantity, midVariant.ReservedQuantity));

        var first = await scope.Deals.ReleaseExpiredDealReservationsAsync();
        var second = await scope.Deals.ReleaseExpiredDealReservationsAsync();
        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var finalVariant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal((30, 0, 0), (finalVariant.AvailableQuantity, finalVariant.ReservedQuantity, finalVariant.SoldQuantity));
        var finalStatus = await scope.Db.B2BDeals.AsNoTracking().Where(d => d.Id == accept.Value).Select(d => d.Status).SingleAsync();
        Assert.Equal(B2BDealStatus.Cancelled, finalStatus);
    }

    [SkippableFact]
    public async Task AdvancingAnExpiredSellerArrangedShippingDealToShipped_IsAlsoRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(
            sellerUserId, start.Value, new AcceptOfferInput(B2BFulfillmentType.SellerArrangedShipping));
        Assert.True(accept.Succeeded, accept.Error);

        await scope.Db.B2BDeals.Where(d => d.Id == accept.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(-5)));

        await using var actionScope = new DealScope(factory);
        var shipped = await actionScope.Deals.MarkShippedAsync(sellerUserId, accept.Value, "WB-1");
        Assert.Equal(ResultErrorKind.Conflict, shipped.ErrorKind);
        var deal = await actionScope.Db.B2BDeals.AsNoTracking().SingleAsync(d => d.Id == accept.Value);
        Assert.Equal(B2BDealStatus.AwaitingFulfillment, deal.Status);
        Assert.Null(deal.ShipmentReference);
    }

    [SkippableFact]
    public async Task AB2BReleaseRacingAB2CReservation_OnDifferentVariants_KeepsTheListingStatusConsistent()
    {
        // Finding 3: a B2B stock release and a concurrent B2C reservation on a *different*
        // variant of the same listing must serialize on the listing row. Deterministic
        // interleave: the B2C order commits inside the release's pre-write gate; the release
        // then conflicts on the listing rowversion, retries, and leaves the listing consistent.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerMerchant, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(sellerUserId);
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 12);
        var consumerId = await scope.CreateUserAsync();
        var listingId = await scope.Db.Listings.AsNoTracking().Where(l => l.Slug == slug).Select(l => l.Id).SingleAsync();

        // variantIds[1] holds a single unit; the deal reserves all of variantIds[0].
        await scope.Db.ListingVariants.Where(v => v.Id == variantIds[1])
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.AvailableQuantity, 1));

        var start = await scope.Negotiations.StartNegotiationAsync(buyerMerchant, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4m, null, null));
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);
        var listingStatusAfterAccept = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.Live, listingStatusAfterAccept);

        await using var orderScope = new DealScope(factory);
        Result<Guid> b2cResult = null!;
        var gatedDeal = scope.NewGatedDealService(async ct =>
            b2cResult = await orderScope.Orders.PlaceOrderAsync(consumerId, new PlaceOrderInput(
                [new OrderLineInput(variantIds[1], 1)],
                OrderFulfillmentType.Pickup, locationId, null, null, "Consumer", "079", null), ct));

        var cancelResult = await gatedDeal.CancelAsync(sellerUserId, accept.Value, "changed our plan");

        Assert.True(b2cResult.Succeeded, b2cResult.Error);
        Assert.True(cancelResult.Failed);
        Assert.Equal(ResultErrorKind.Conflict, cancelResult.ErrorKind);

        // The B2C order flipped the listing to SoldOut; the deal is still reserved.
        await using (var verify = scope.CreateDbContext())
        {
            Assert.Equal(ListingStatus.SoldOut,
                await verify.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync());
        }

        // The loser retries against current state and the release now legitimately republishes.
        await using (var retry = new DealScope(factory))
        {
            var retryCancel = await retry.Deals.CancelAsync(sellerUserId, accept.Value, "changed our plan");
            Assert.True(retryCancel.Succeeded, retryCancel.Error);
        }

        await using var final = scope.CreateDbContext();
        Assert.Equal(ListingStatus.Live,
            await final.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync());
        var v0 = await final.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        var v1 = await final.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[1]);
        Assert.Equal((12, 0, 0), (v0.AvailableQuantity, v0.ReservedQuantity, v0.SoldQuantity));
        Assert.Equal(0, v1.AvailableQuantity);
    }

    [SkippableFact]
    public async Task AcceptOffer_SnapshotsExactlyTheAgreedTerms_AndAddsNoShippingCharge()
    {
        // Finding 4: the deal snapshots the agreed unit price and quantities only; the total
        // is the agreed subtotal, with no shipping charge and no shipment reference attached
        // at acceptance (docs/17-DATA-INVARIANTS.md, docs/03-BUSINESS-RULES.md §12).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new DealScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 30);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12), new B2BOfferLineInput(variantIds[1], 13)], 4.250m, null, null));
        Assert.True(start.Succeeded, start.Error);
        var accept = await scope.Deals.AcceptOfferAsync(sellerUserId, start.Value, PickupAccept);
        Assert.True(accept.Succeeded, accept.Error);

        var revision = await scope.Db.B2BOfferRevisions.AsNoTracking()
            .Include(r => r.Lines).SingleAsync(r => r.B2BNegotiationId == start.Value);
        var deal = await scope.Db.B2BDeals.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.Id == accept.Value);

        Assert.Equal(revision.ProposedUnitPrice, deal.AcceptedUnitPriceSnapshot);
        Assert.Equal(revision.ProposedTotal, deal.SubtotalSnapshot);
        Assert.Equal(deal.SubtotalSnapshot, deal.TotalSnapshot);
        Assert.Null(deal.ShippingCostSnapshot);
        Assert.Null(deal.ShipmentReference);
        Assert.All(deal.Lines, l => Assert.Equal(revision.ProposedUnitPrice, l.UnitPriceSnapshot));
        Assert.Equal(revision.TotalQuantity, deal.TotalUnits);
    }

    private sealed class DealScope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];
        private readonly List<ApplicationDbContext> _extraContexts = [];

        public IB2BNegotiationService Negotiations => _scope.ServiceProvider.GetRequiredService<IB2BNegotiationService>();

        public IB2BDealService Deals => _scope.ServiceProvider.GetRequiredService<IB2BDealService>();

        public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

        public IMerchantStoreService Store => _scope.ServiceProvider.GetRequiredService<IMerchantStoreService>();

        public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

        public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        public ApplicationDbContext CreateDbContext()
        {
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(Db.Database.GetConnectionString()
                    ?? throw new InvalidOperationException("The test DbContext has no connection string."))
                .Options);
            _extraContexts.Add(context);
            return context;
        }

        public B2BDealService NewGatedDealService(Func<CancellationToken, Task> beforeFirstSave) => new(
            new GatedApplicationDbContext(CreateDbContext(), beforeFirstSave),
            _scope.ServiceProvider.GetRequiredService<IClock>(),
            _scope.ServiceProvider.GetRequiredService<IUserRoleService>(),
            _scope.ServiceProvider.GetRequiredService<IOptions<B2BDealOptions>>(),
            _scope.ServiceProvider.GetRequiredService<ILogger<B2BDealService>>());

        public async Task<string> CreateUserAsync(string? role = null)
        {
            var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = $"{Guid.NewGuid():N}@test.local",
                Email = $"{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
            };
            Assert.True((await users.CreateAsync(user)).Succeeded);

            if (role is not null)
            {
                var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }

                await users.AddToRoleAsync(user, role);
            }

            return user.Id;
        }

        public async Task<(string UserId, Guid MerchantProfileId)> CreateApprovedMerchantAsync()
        {
            var userId = await CreateUserAsync();
            var now = DateTime.UtcNow;
            var profile = new MerchantProfile(userId, $"Test Merchant {Guid.NewGuid():N}", $"test-{Guid.NewGuid():N}", now);
            profile.AddDocument(MerchantVerificationDocumentType.CommercialRegistration, "test-key", "reg.pdf", "application/pdf", 10, now);
            profile.SubmitForReview(now);
            profile.Approve("test-admin-seed", now);
            Db.MerchantProfiles.Add(profile);
            await Db.SaveChangesAsync();
            _merchantProfileIds.Add(profile.Id);
            return (userId, profile.Id);
        }

        public async Task SuspendMerchantAsync(Guid merchantProfileId)
        {
            var profile = await Db.MerchantProfiles.SingleAsync(p => p.Id == merchantProfileId);
            profile.Suspend("test-admin", "compliance hold", DateTime.UtcNow);
            await Db.SaveChangesAsync();
        }

        public async Task<Guid> AddPickupLocationAsync(string merchantUserId)
        {
            var result = await Store.AddLocationAsync(merchantUserId, new MerchantLocationInput(
                "Main store", "1 King St", "Abdali", "Amman", "Ring the bell", "Sun–Thu 10:00–18:00"));
            Assert.True(result.Succeeded, result.Error);
            return result.Value;
        }

        public async Task<(string Slug, IReadOnlyList<Guid> VariantIds)> CreateLiveB2BListingAsync(
            string merchantUserId, int moq, int initialQuantity)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var details = new ListingDetailsInput(
                categoryId, null, gradeId, "Wholesale T-Shirts", "Overstock cotton tees.",
                null, 9.000m, 5.000m, moq, true, true, true, null, null, null, null, []);

            var create = await Listings.CreateAsync(merchantUserId, details);
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();

            foreach (var value in new[] { "M", "L" })
            {
                Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, value)).Succeeded);
                var valueId = await Db.Set<ListingOptionValue>()
                    .Where(v => v.ListingOptionId == optionId && v.Value == value).Select(v => v.Id).SingleAsync();
                Assert.True((await Listings.AddVariantAsync(
                    merchantUserId, listingId, new AddVariantInput($"TEE-{value}-{Guid.NewGuid():N}", [valueId], initialQuantity))).Succeeded);
            }

            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
            Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, details with { DiscountReasonIds = [reasonId] })).Succeeded);
            Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
            var adminId = await CreateUserAsync(FaedRoles.Admin);
            Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

            var ordered = await Db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).OrderBy(v => v.Sku).Select(v => v.Id).ToListAsync();
            var slug = await Db.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
            _listingIds.Add(listingId);
            return (slug, ordered);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var context in _extraContexts)
            {
                await context.DisposeAsync();
            }

            if (_listingIds.Count > 0 || _merchantProfileIds.Count > 0)
            {
                await using var cleanupDb = new ApplicationDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseSqlServer(Db.Database.GetConnectionString()
                            ?? throw new InvalidOperationException("The test DbContext has no connection string."))
                        .Options);

                var merchantIds = _merchantProfileIds;
                var listingIds = _listingIds;

                cleanupDb.B2BDeals.RemoveRange(
                    await cleanupDb.B2BDeals.Where(d => merchantIds.Contains(d.SellingMerchantProfileId)
                        || merchantIds.Contains(d.BuyingMerchantProfileId)).ToListAsync());
                await cleanupDb.SaveChangesAsync();

                cleanupDb.B2BNegotiations.RemoveRange(
                    await cleanupDb.B2BNegotiations.Where(n => merchantIds.Contains(n.SellingMerchantProfileId)
                        || merchantIds.Contains(n.BuyingMerchantProfileId)).ToListAsync());
                await cleanupDb.SaveChangesAsync();

                cleanupDb.Orders.RemoveRange(
                    await cleanupDb.Orders.Where(o => merchantIds.Contains(o.MerchantProfileId)).ToListAsync());
                await cleanupDb.SaveChangesAsync();

                cleanupDb.MerchantLocations.RemoveRange(
                    cleanupDb.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)));
                await cleanupDb.SaveChangesAsync();

                if (listingIds.Count > 0)
                {
                    var variantIds = await cleanupDb.ListingVariants
                        .Where(v => listingIds.Contains(v.ListingId)).Select(v => v.Id).ToListAsync();
                    cleanupDb.InventoryAdjustments.RemoveRange(
                        cleanupDb.InventoryAdjustments.Where(a => variantIds.Contains(a.ListingVariantId)));
                    await cleanupDb.SaveChangesAsync();
                    cleanupDb.Listings.RemoveRange(
                        await cleanupDb.Listings.Where(l => listingIds.Contains(l.Id)).ToListAsync());
                    await cleanupDb.SaveChangesAsync();
                }

                if (merchantIds.Count > 0)
                {
                    cleanupDb.MerchantProfiles.RemoveRange(
                        await cleanupDb.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)).ToListAsync());
                    await cleanupDb.SaveChangesAsync();
                }
            }

            _scope.Dispose();
        }
    }
}
