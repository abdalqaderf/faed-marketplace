using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Marketplace;
using Faed.Web.Services.Ordering;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.IntegrationTests;

/// <summary>
/// B2C ordering against real SQL Server (tasks/TASK-006-B2C-ORDERS.md "Mandatory tests" and
/// "Exit criteria"; docs/09-TEST-STRATEGY.md §2-3 — reservation concurrency is proven
/// against SQL Server, never InMemory/SQLite).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class OrderServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task PlaceOrder_ComputesTotalsServerSide_AndSnapshotsSurviveAListingRepricing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (listingId, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 10, retailPrice: 12.500m);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 3)],
            OrderFulfillmentType.Pickup, locationId, null, null, "Buyer One", "0790000000", null));
        Assert.True(placed.Succeeded, placed.Error);

        var order = await scope.Db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == placed.Value);
        Assert.Equal(37.500m, order.Subtotal);
        Assert.Equal(37.500m, order.Total);
        Assert.Equal(12.500m, order.Items.Single().UnitPriceSnapshot);
        Assert.Equal(37.500m, order.Items.Single().LineTotalSnapshot);

        // Repricing the listing afterwards must not rewrite the order snapshot.
        await scope.Db.Listings.Where(l => l.Id == listingId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RetailPrice, 99.999m));

        var reread = await scope.Db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == placed.Value);
        Assert.Equal(12.500m, reread.Items.Single().UnitPriceSnapshot);
    }

    [SkippableFact]
    public async Task PlaceOrder_WithVariantsFromTwoMerchants_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantA, _) = await scope.CreateApprovedMerchantAsync();
        var (merchantB, _) = await scope.CreateApprovedMerchantAsync();
        await scope.AddPickupLocationAsync(merchantA);
        var locB = await scope.AddPickupLocationAsync(merchantB);
        var (_, variantA) = await scope.CreateLiveListingAsync(merchantA);
        var (_, variantB) = await scope.CreateLiveListingAsync(merchantB);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantA, 1), new OrderLineInput(variantB, 1)],
            OrderFulfillmentType.Pickup, locB, null, null, "Buyer", "079", null));

        Assert.True(placed.Failed);
        Assert.Contains("same merchant", placed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await scope.Db.Orders.AsNoTracking().Where(o => o.BuyerUserId == buyerId).ToListAsync());
    }

    [SkippableFact]
    public async Task PlaceOrder_TwoBuyersCompeteForTheLastUnit_TheLoserGetsAConcurrencyConflict()
    {
        // Deterministic interleaving (not Task.WhenAll): order A pauses immediately before its
        // write, order B runs to completion and commits against the *same* original stock
        // state (Available = 1, same variant rowversion), then A's write is released and must
        // fail on the moved token (docs/09-TEST-STRATEGY.md §2, docs/05 §9).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 1);
        var buyerA = await scope.CreateUserAsync();
        var buyerB = await scope.CreateUserAsync();
        var input = PickupInput(variantId, 1, locationId);

        await using var scopeB = new OrderScope(factory);
        Result<Guid> resultB = null!;
        var serviceA = scope.NewGatedOrderService(async ct =>
            resultB = await scopeB.Orders.PlaceOrderAsync(buyerB, input, ct));

        var resultA = await serviceA.PlaceOrderAsync(buyerA, input);

        Assert.True(resultB.Succeeded, resultB.Error);
        Assert.True(resultA.Failed);
        Assert.Equal(ResultErrorKind.Conflict, resultA.ErrorKind);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(0, variant.AvailableQuantity);
        Assert.Equal(1, variant.ReservedQuantity);
        Assert.Equal(1, await scope.Db.Orders.AsNoTracking()
            .CountAsync(o => o.BuyerUserId == buyerA || o.BuyerUserId == buyerB));
    }

    [SkippableFact]
    public async Task PlaceOrder_TwoConcurrentOrdersDepletingDifferentVariants_LeaveTheListingSoldOut_NotLive()
    {
        // The multi-variant race: without forcing the listing row into each reserving
        // transaction, two orders each emptying a *different* single-unit variant would both
        // commit against a listing each still sees as in stock, leaving it wrongly Live
        // (docs/17-DATA-INVARIANTS.md, docs/05 §2). Deterministic interleave: B commits inside
        // A's pre-write gate; A must then conflict on the listing rowversion B advanced.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (listingId, variantIds) = await scope.CreateLiveListingWithVariantsAsync(merchantUserId, 1, 1);
        var buyerA = await scope.CreateUserAsync();
        var buyerB = await scope.CreateUserAsync();

        await using var scopeB = new OrderScope(factory);
        Result<Guid> resultB = null!;
        var serviceA = scope.NewGatedOrderService(async ct =>
            resultB = await scopeB.Orders.PlaceOrderAsync(buyerB, PickupInput(variantIds[1], 1, locationId), ct));

        var resultA = await serviceA.PlaceOrderAsync(buyerA, PickupInput(variantIds[0], 1, locationId));

        Assert.True(resultB.Succeeded, resultB.Error);
        Assert.True(resultA.Failed);
        Assert.Equal(ResultErrorKind.Conflict, resultA.ErrorKind);

        // The loser retries (fresh scope, like a real second request) against current state
        // and now legitimately depletes the listing.
        await using (var retryScope = new OrderScope(factory))
        {
            var retryA = await retryScope.Orders.PlaceOrderAsync(buyerA, PickupInput(variantIds[0], 1, locationId));
            Assert.True(retryA.Succeeded, retryA.Error);
        }

        await using var verifyDb = scope.CreateDbContext();
        var status = await verifyDb.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.SoldOut, status);
        var variants = await verifyDb.ListingVariants.AsNoTracking()
            .Where(v => v.ListingId == listingId).ToListAsync();
        Assert.All(variants, v => Assert.Equal(0, v.AvailableQuantity));
    }

    [SkippableFact]
    public async Task Confirm_WhenTheReservationHasExpired_IsRejected_AndTheSweepThenReleasesTheStock()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 2, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        await scope.Db.Orders.Where(o => o.Id == placed.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        // A merchant hitting "confirm" on an order whose window already lapsed is refused.
        await using (var confirmScope = new OrderScope(factory))
        {
            var confirm = await confirmScope.Orders.ConfirmAsync(merchantUserId, placed.Value);
            Assert.True(confirm.Failed);
            Assert.Equal(ResultErrorKind.Conflict, confirm.ErrorKind);
        }

        var midStatus = await scope.Db.Orders.AsNoTracking()
            .Where(o => o.Id == placed.Value).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Pending, midStatus);

        var released = await scope.Orders.ReleaseExpiredReservationsAsync();
        Assert.Equal(1, released);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(5, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
        var finalStatus = await scope.Db.Orders.AsNoTracking()
            .Where(o => o.Id == placed.Value).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, finalStatus);
    }

    [SkippableFact]
    public async Task PlaceOrder_ByAnAdministrator_IsForbidden_ServerSide()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var placed = await scope.Orders.PlaceOrderAsync(adminId, PickupInput(variantId, 1, locationId));

        Assert.True(placed.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, placed.ErrorKind);
        Assert.Empty(await scope.Db.Orders.AsNoTracking().Where(o => o.BuyerUserId == adminId).ToListAsync());

        var checkout = await scope.Orders.GetCheckoutAsync(adminId, await scope.SlugForVariantAsync(variantId));
        Assert.True(checkout.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, checkout.ErrorKind);
    }

    [SkippableFact]
    public async Task PlaceOrder_ByARolelessAccount_IsForbidden_ServerSide()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var rolelessUserId = await scope.CreateUserAsync(role: null);

        var placed = await scope.Orders.PlaceOrderAsync(
            rolelessUserId,
            PickupInput(variantId, 1, locationId));

        Assert.True(placed.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, placed.ErrorKind);
        Assert.Empty(await scope.Db.Orders.AsNoTracking()
            .Where(order => order.BuyerUserId == rolelessUserId)
            .ToListAsync());

        var checkout = await scope.Orders.GetCheckoutAsync(
            rolelessUserId,
            await scope.SlugForVariantAsync(variantId));
        Assert.True(checkout.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, checkout.ErrorKind);
    }

    [SkippableFact]
    public async Task Order_BuyerUserId_IsReferentiallyBoundToAnIdentityUser()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, merchantId) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (listingId, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 1, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        var order = await scope.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == placed.Value);
        Assert.True(await scope.Db.Users.AsNoTracking().AnyAsync(u => u.Id == order.BuyerUserId));

        // The database rejects an order whose buyer id is not a real Identity user.
        await using var raw = scope.CreateDbContext();
        var orphan = new Order(
            "not-a-real-user", merchantId, OrderFulfillmentType.Pickup, locationId, null, 0m,
            "Main store", null, "Someone", "079", null, DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        orphan.AddItem(listingId, variantId, 1, 10m, "Sneakers", "Size: M", "Grade A", null);
        raw.Orders.Add(orphan);
        await Assert.ThrowsAsync<DbUpdateException>(() => raw.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task ConfirmReceipt_ByTheBuyer_CompletesTheOrder_AndMovesReservedStockToSold()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();
        var otherBuyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 2, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        // Too early: not yet handed over.
        Assert.True((await scope.Orders.ConfirmReceiptAsync(buyerId, placed.Value)).Failed);

        Assert.True((await scope.Orders.ConfirmAsync(merchantUserId, placed.Value)).Succeeded);
        Assert.True((await scope.Orders.ConfirmReceiptAsync(buyerId, placed.Value)).Failed); // still Confirmed
        Assert.True((await scope.Orders.MarkReadyForPickupAsync(merchantUserId, placed.Value)).Succeeded);

        // Not the buyer's order.
        Assert.True((await scope.Orders.ConfirmReceiptAsync(otherBuyerId, placed.Value)).Failed);

        var receipt = await scope.Orders.ConfirmReceiptAsync(buyerId, placed.Value);
        Assert.True(receipt.Succeeded, receipt.Error);

        var status = await scope.Db.Orders.AsNoTracking()
            .Where(o => o.Id == placed.Value).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Completed, status);
        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(3, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
        Assert.Equal(2, variant.SoldQuantity);
    }

    [SkippableFact]
    public async Task Checkout_AndPlaceOrder_WithAMaximumLengthPickupLocation_DoNotFail()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        var add = await scope.Store.AddLocationAsync(merchantUserId, new MerchantLocationInput(
            new string('N', MerchantLocation.MaxNameLength),
            new string('A', MerchantLocation.MaxAddressLineLength),
            new string('R', MerchantLocation.MaxAreaLength),
            new string('C', MerchantLocation.MaxCityLength),
            new string('I', MerchantLocation.MaxInstructionsLength),
            new string('H', MerchantLocation.MaxHoursLength)));
        Assert.True(add.Succeeded, add.Error);

        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 1, add.Value));
        Assert.True(placed.Succeeded, placed.Error);

        var order = await scope.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == placed.Value);
        Assert.True(order.FulfillmentSnapshot.Length <= Order.MaxFulfillmentSnapshotLength);
        Assert.NotEmpty(order.FulfillmentSnapshot);
    }

    [SkippableFact]
    public async Task CancelOrder_ReleasesReservedStock()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 2, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        var afterReserve = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(3, afterReserve.AvailableQuantity);
        Assert.Equal(2, afterReserve.ReservedQuantity);

        var cancel = await scope.Orders.CancelMyOrderAsync(buyerId, placed.Value, "changed my mind");
        Assert.True(cancel.Succeeded, cancel.Error);

        var afterCancel = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(5, afterCancel.AvailableQuantity);
        Assert.Equal(0, afterCancel.ReservedQuantity);
        Assert.Equal(0, afterCancel.SoldQuantity);
    }

    [SkippableFact]
    public async Task CompleteOrder_MovesReservedStockToSold()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 2, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        Assert.True((await scope.Orders.ConfirmAsync(merchantUserId, placed.Value)).Succeeded);
        Assert.True((await scope.Orders.MarkReadyForPickupAsync(merchantUserId, placed.Value)).Succeeded);
        Assert.True((await scope.Orders.CompleteAsync(merchantUserId, placed.Value)).Succeeded);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(3, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);
        Assert.Equal(2, variant.SoldQuantity);

        var status = await scope.Db.Orders.AsNoTracking().Where(o => o.Id == placed.Value).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Completed, status);
    }

    [SkippableFact]
    public async Task MerchantDelivery_AddsTheZoneFeeToTheTotal_AndEnforcesTheZoneMinimum()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var zoneId = await scope.AddDeliveryZoneAsync(merchantUserId, fee: 2.500m, minimumOrderValue: 20m);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 10, retailPrice: 6m);
        var buyerId = await scope.CreateUserAsync();

        // 2 units * JOD 6 = JOD 12 — below the JOD 20 zone minimum.
        var tooSmall = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 2)],
            OrderFulfillmentType.MerchantDelivery, null, zoneId, "5 Rainbow St, Amman", "Buyer", "079", null));
        Assert.True(tooSmall.Failed);
        Assert.Contains("minimum", tooSmall.Error, StringComparison.OrdinalIgnoreCase);

        // 4 units * JOD 6 = JOD 24 subtotal, + JOD 2.5 delivery = JOD 26.5 total.
        var ok = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 4)],
            OrderFulfillmentType.MerchantDelivery, null, zoneId, "5 Rainbow St, Amman", "Buyer", "079", null));
        Assert.True(ok.Succeeded, ok.Error);

        var order = await scope.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == ok.Value);
        Assert.Equal(24.000m, order.Subtotal);
        Assert.Equal(2.500m, order.DeliveryFeeSnapshot);
        Assert.Equal(26.500m, order.Total);
        Assert.Equal(OrderFulfillmentType.MerchantDelivery, order.FulfillmentType);
    }

    [SkippableFact]
    public async Task OrderDetail_IsPrivateToItsBuyer_AndItsSellingMerchant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (otherMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();
        var otherBuyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 1, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        Assert.NotNull(await scope.Orders.GetMyOrderAsync(buyerId, placed.Value));
        Assert.Null(await scope.Orders.GetMyOrderAsync(otherBuyerId, placed.Value));

        Assert.NotNull(await scope.Orders.GetMerchantOrderAsync(merchantUserId, placed.Value));
        Assert.Null(await scope.Orders.GetMerchantOrderAsync(otherMerchantUserId, placed.Value));

        // Another merchant cannot drive its fulfilment either.
        Assert.True((await scope.Orders.ConfirmAsync(otherMerchantUserId, placed.Value)).Failed);
    }

    [SkippableFact]
    public async Task ExpiredReservation_IsReleasedByTheSweep_ExactlyOnce()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 4);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 3, locationId));
        Assert.True(placed.Succeeded, placed.Error);

        // Force the reservation window into the past.
        await scope.Db.Orders.Where(o => o.Id == placed.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ReservationExpiresAtUtc, DateTime.UtcNow.AddMinutes(-5)));

        var firstSweep = await scope.Orders.ReleaseExpiredReservationsAsync();
        var secondSweep = await scope.Orders.ReleaseExpiredReservationsAsync();

        Assert.Equal(1, firstSweep);
        Assert.Equal(0, secondSweep);

        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(4, variant.AvailableQuantity);
        Assert.Equal(0, variant.ReservedQuantity);

        var status = await scope.Db.Orders.AsNoTracking().Where(o => o.Id == placed.Value).Select(o => o.Status).SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, status);
    }

    [SkippableFact]
    public async Task PlaceOrder_AgainstASuspendedMerchant_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new OrderScope(factory);
        var (merchantUserId, merchantId) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await scope.CreateLiveListingAsync(merchantUserId, initialQuantity: 5);
        var buyerId = await scope.CreateUserAsync();

        var profile = await scope.Db.MerchantProfiles.SingleAsync(p => p.Id == merchantId);
        profile.Suspend("admin", "compliance hold", DateTime.UtcNow);
        await scope.Db.SaveChangesAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, PickupInput(variantId, 1, locationId));
        Assert.True(placed.Failed);
    }

    private static PlaceOrderInput PickupInput(Guid variantId, int quantity, Guid locationId) => new(
        [new OrderLineInput(variantId, quantity)],
        OrderFulfillmentType.Pickup, locationId, null, null, "Buyer One", "0790000000", null);

    private sealed class OrderScope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];
        private readonly List<ApplicationDbContext> _extraContexts = [];

        public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

        public IMerchantStoreService Store => _scope.ServiceProvider.GetRequiredService<IMerchantStoreService>();

        public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

        public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        /// <summary>A fresh, independently-tracked context on the shared test connection.</summary>
        public ApplicationDbContext CreateDbContext()
        {
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(Db.Database.GetConnectionString()
                    ?? throw new InvalidOperationException("The test DbContext has no connection string."))
                .Options);
            _extraContexts.Add(context);
            return context;
        }

        /// <summary>
        /// An <see cref="OrderService"/> wired over a <see cref="GatedApplicationDbContext"/> so a
        /// test can interleave it deterministically with a competing order.
        /// </summary>
        public OrderService NewGatedOrderService(Func<CancellationToken, Task> beforeFirstSave) => new(
            new GatedApplicationDbContext(CreateDbContext(), beforeFirstSave),
            _scope.ServiceProvider.GetRequiredService<IPublicMarketplaceService>(),
            _scope.ServiceProvider.GetRequiredService<IUserRoleService>(),
            _scope.ServiceProvider.GetRequiredService<IClock>(),
            _scope.ServiceProvider.GetRequiredService<IOptions<OrderingOptions>>(),
            _scope.ServiceProvider.GetRequiredService<ILogger<OrderService>>());

        public Task<string> SlugForVariantAsync(Guid variantId) =>
            Db.ListingVariants.AsNoTracking()
                .Where(v => v.Id == variantId)
                .Join(Db.Listings.AsNoTracking(), v => v.ListingId, l => l.Id, (_, l) => l.Slug)
                .SingleAsync();

        public async Task<string> CreateUserAsync(string? role = FaedRoles.Buyer)
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

        public async Task<Guid> AddPickupLocationAsync(string merchantUserId)
        {
            var result = await Store.AddLocationAsync(merchantUserId, new MerchantLocationInput(
                "Main store", "1 King St", "Abdali", "Amman", "Ring the bell", "Sun–Thu 10:00–18:00"));
            Assert.True(result.Succeeded, result.Error);
            return result.Value;
        }

        public async Task<Guid> AddDeliveryZoneAsync(string merchantUserId, decimal fee, decimal? minimumOrderValue = null)
        {
            var result = await Store.AddDeliveryZoneAsync(merchantUserId, new MerchantDeliveryZoneInput(
                "West Amman", fee, minimumOrderValue, "1–2 days"));
            Assert.True(result.Succeeded, result.Error);
            return result.Value;
        }

        public async Task<(Guid ListingId, Guid VariantId)> CreateLiveListingAsync(
            string merchantUserId, int initialQuantity = 5, decimal retailPrice = 24.500m)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(merchantUserId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, retailPrice, null, null, true, false, false, null, null, null, null, []));
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
            Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, "M")).Succeeded);
            var valueId = await Db.Set<ListingOptionValue>().Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();
            Assert.True((await Listings.AddVariantAsync(
                merchantUserId, listingId, new AddVariantInput($"SNK-{Guid.NewGuid():N}", [valueId], initialQuantity))).Succeeded);
            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
            Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, retailPrice, null, null, true, false, false, null, null, null, null, [reasonId]))).Succeeded);

            Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
            var adminId = await CreateUserAsync(FaedRoles.Admin);
            Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

            var variantId = await Db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();

            _listingIds.Add(listingId);
            return (listingId, variantId);
        }

        /// <summary>A Live listing with one <c>Size</c> option carrying one value per requested
        /// quantity and a single-value variant for each — so a test can deplete each variant
        /// independently. Returns the listing id and the variant ids in the same order as
        /// <paramref name="quantities"/>.</summary>
        public async Task<(Guid ListingId, IReadOnlyList<Guid> VariantIds)> CreateLiveListingWithVariantsAsync(
            string merchantUserId, params int[] quantities)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(merchantUserId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null, true, false, false, null, null, null, null, []));
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();

            var variantIds = new List<Guid>();
            for (var i = 0; i < quantities.Length; i++)
            {
                var value = $"SZ{i}";
                Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, value)).Succeeded);
                var valueId = await Db.Set<ListingOptionValue>()
                    .Where(v => v.ListingOptionId == optionId && v.Value == value).Select(v => v.Id).SingleAsync();
                var sku = $"SNK-{i}-{Guid.NewGuid():N}";
                Assert.True((await Listings.AddVariantAsync(
                    merchantUserId, listingId, new AddVariantInput(sku, [valueId], quantities[i]))).Succeeded);
            }

            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
            Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null, true, false, false, null, null, null, null, [reasonId]))).Succeeded);
            Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
            var adminId = await CreateUserAsync(FaedRoles.Admin);
            Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

            var ordered = await Db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId)
                .OrderBy(v => v.Sku)
                .Select(v => v.Id)
                .ToListAsync();

            _listingIds.Add(listingId);
            return (listingId, ordered);
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

                var listingIds = _listingIds;
                var merchantIds = _merchantProfileIds;

                var orders = await cleanupDb.Orders
                    .Where(o => merchantIds.Contains(o.MerchantProfileId))
                    .ToListAsync();
                cleanupDb.Orders.RemoveRange(orders);
                await cleanupDb.SaveChangesAsync();

                var locations = cleanupDb.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId));
                cleanupDb.MerchantLocations.RemoveRange(locations);
                var zones = cleanupDb.MerchantDeliveryZones.Where(z => merchantIds.Contains(z.MerchantProfileId));
                cleanupDb.MerchantDeliveryZones.RemoveRange(zones);
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
