using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Marketplace;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faed.Web.Tests.Services.Ordering;

/// <summary>
/// PHASE-PLAN.md Phase 1, test 4: an unconfirmed order past its reservation window is
/// cancelled and its stock released. Extended for Phase 9, whose
/// <see cref="OrderDeadlineService"/> adds the 48-hour no-show and 72-hour auto-close
/// deadlines on the same pattern — a regression here would only otherwise surface by hand.
/// </summary>
public class ReservationExpiryTests
{
    [Fact]
    public async Task ExpiredReservation_IsCancelled_AndStockReleased()
    {
        var options = CreateOptions();

        await using var db = new ApplicationDbContext(options);
        var clock = new TestClock(DateTime.UtcNow);
        var (variant, _) = await SeedReservedOrderAsync(db, clock, initialQuantity: 5, reservedQuantity: 2);

        var orderService = new OrderService(
            db, new ThrowingMarketplaceService(), new AllowAllUserRoleService(), clock,
            Options.Create(new OrderingOptions()), NullLogger<OrderService>.Instance);

        var released = await orderService.ReleaseExpiredReservationsAsync();

        Assert.Equal(1, released);

        var reloadedOrder = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, reloadedOrder.Status);

        var reloadedVariant = await db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(5, reloadedVariant.AvailableQuantity);
        Assert.Equal(0, reloadedVariant.ReservedQuantity);
    }

    /// <summary>
    /// The InMemory provider does not generate rowversion values the way SQL Server does, so a
    /// genuine oversell/concurrency race cannot be provoked against it (PHASE-PLAN.md Phase 1).
    /// This instead verifies <see cref="OrderService"/>'s own handling of the exception a real
    /// conflict would raise: a save forced to throw <see cref="DbUpdateConcurrencyException"/>
    /// must be treated as "already handled elsewhere" — released count unaffected, order left
    /// untouched for the next sweep — never allowed to propagate or double-release stock.
    /// </summary>
    [Fact]
    public async Task ExpiredReservation_ConcurrentUpdate_IsTreatedAsAlreadyHandled()
    {
        var options = CreateOptions();

        var clock = new TestClock(DateTime.UtcNow);
        await using (var seedDb = new ApplicationDbContext(options))
        {
            await SeedReservedOrderAsync(seedDb, clock, initialQuantity: 5, reservedQuantity: 2);
        }

        await using var throwingDb = new ThrowOnceOnSaveDbContext(options) { ThrowNextSave = true };
        var orderService = new OrderService(
            throwingDb, new ThrowingMarketplaceService(), new AllowAllUserRoleService(), clock,
            Options.Create(new OrderingOptions()), NullLogger<OrderService>.Instance);

        var released = await orderService.ReleaseExpiredReservationsAsync();

        Assert.Equal(0, released);

        await using var verifyDb = new ApplicationDbContext(options);
        var order = await verifyDb.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.Pending, order.Status);

        var variant = await verifyDb.ListingVariants.AsNoTracking().SingleAsync();
        Assert.Equal(2, variant.ReservedQuantity);
    }

    /// <summary>
    /// <see cref="OrderService"/> wraps every status transition in a database transaction; the
    /// InMemory provider does not support transactions and raises that as an error unless the
    /// warning is explicitly ignored, which is safe here since InMemory only ever runs single-
    /// threaded against one in-process store within a test.
    /// </summary>
    private static DbContextOptions<ApplicationDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    [Fact]
    public async Task ConfirmedOrder_NotHandedOverInTime_BecomesNoShow_AndStockReleased()
    {
        var options = CreateOptions();
        await using var db = new ApplicationDbContext(options);
        var clock = new TestClock(DateTime.UtcNow);

        var (variant, order) = await SeedReservedOrderAsync(
            db, clock, initialQuantity: 5, reservedQuantity: 2, reservationExpiresAtUtc: clock.UtcNow.AddHours(12));
        order.Confirm(clock.UtcNow);
        await db.SaveChangesAsync();

        var orderService = NewOrderService(db, clock);

        // A minute before the 48-hour deadline: nothing happens.
        clock.UtcNow = clock.UtcNow.AddHours(48).AddMinutes(-1);
        Assert.Equal(0, await orderService.ExpireUnhandledConfirmedOrdersAsync());

        // Past it: the order is a no-show and the two reserved units are back on sale.
        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        Assert.Equal(1, await orderService.ExpireUnhandledConfirmedOrdersAsync());

        var reloaded = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.NoShow, reloaded.Status);
        var reloadedVariant = await db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(5, reloadedVariant.AvailableQuantity);
        Assert.Equal(0, reloadedVariant.ReservedQuantity);
    }

    [Fact]
    public async Task ReadyOrder_LeftUncollected_IsAutoClosed_AndStockReleased()
    {
        var options = CreateOptions();
        await using var db = new ApplicationDbContext(options);
        var clock = new TestClock(DateTime.UtcNow);

        var (variant, order) = await SeedReservedOrderAsync(
            db, clock, initialQuantity: 5, reservedQuantity: 2, reservationExpiresAtUtc: clock.UtcNow.AddHours(12));
        order.Confirm(clock.UtcNow);
        order.MarkReadyForPickup(clock.UtcNow);
        await db.SaveChangesAsync();

        var orderService = NewOrderService(db, clock);

        clock.UtcNow = clock.UtcNow.AddHours(71);
        Assert.Equal(0, await orderService.CloseStaleReadyOrdersAsync());

        clock.UtcNow = clock.UtcNow.AddHours(2);
        Assert.Equal(1, await orderService.CloseStaleReadyOrdersAsync());

        var reloaded = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.NoShow, reloaded.Status);
        var reloadedVariant = await db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(0, reloadedVariant.ReservedQuantity);
    }

    [Fact]
    public async Task NoShowOrder_MarkedActuallyCollected_BecomesCompleted_AndUnlocksReview()
    {
        var options = CreateOptions();
        await using var db = new ApplicationDbContext(options);
        var clock = new TestClock(DateTime.UtcNow);

        var (variant, order) = await SeedReservedOrderAsync(
            db, clock, initialQuantity: 5, reservedQuantity: 2, reservationExpiresAtUtc: clock.UtcNow.AddHours(12));
        order.Confirm(clock.UtcNow);
        await db.SaveChangesAsync();

        var orderService = NewOrderService(db, clock);

        // The 48-hour sweep closes it as a no-show and puts the two units back on sale.
        clock.UtcNow = clock.UtcNow.AddHours(49);
        Assert.Equal(1, await orderService.ExpireUnhandledConfirmedOrdersAsync());
        Assert.Equal(OrderStatus.NoShow, (await db.Orders.AsNoTracking().SingleAsync()).Status);

        // The sale did happen — the merchant just never opened the dashboard. Recover it.
        var recovered = await orderService.MarkActuallyCollectedAsync("merchant-user", order.Id);
        Assert.True(recovered.Succeeded);

        var reloadedOrder = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(OrderStatus.Completed, reloadedOrder.Status);
        Assert.NotNull(reloadedOrder.CompletedAtUtc);

        var reloadedVariant = await db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(2, reloadedVariant.SoldQuantity);
        Assert.Equal(0, reloadedVariant.ReservedQuantity);
        Assert.Equal(3, reloadedVariant.AvailableQuantity);

        // The buyer's review now unlocks exactly as after a normal completion.
        var reviews = new ReviewService(
            db, new AllowAllUserRoleService(), clock, NullLogger<ReviewService>.Instance);
        var review = await reviews.SubmitReviewAsync(
            "buyer-user", new SubmitReviewInput(order.Id, 5, "Collected and paid — all fine."));
        Assert.True(review.Succeeded);
    }

    [Fact]
    public async Task MarkActuallyCollected_ByABuyer_IsForbidden_AndLeavesTheNoShow()
    {
        var options = CreateOptions();
        await using var db = new ApplicationDbContext(options);
        var clock = new TestClock(DateTime.UtcNow);

        var (_, order) = await SeedReservedOrderAsync(
            db, clock, initialQuantity: 5, reservedQuantity: 2, reservationExpiresAtUtc: clock.UtcNow.AddHours(12));
        order.Confirm(clock.UtcNow);
        await db.SaveChangesAsync();

        var orderService = NewOrderService(db, clock);
        clock.UtcNow = clock.UtcNow.AddHours(49);
        await orderService.ExpireUnhandledConfirmedOrdersAsync();

        // "buyer-user" is not an approved merchant, so the merchant-only recovery is refused.
        var result = await orderService.MarkActuallyCollectedAsync("buyer-user", order.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
        Assert.Equal(OrderStatus.NoShow, (await db.Orders.AsNoTracking().SingleAsync()).Status);
    }

    private static OrderService NewOrderService(ApplicationDbContext db, TestClock clock) =>
        new(db, new ThrowingMarketplaceService(), new AllowAllUserRoleService(), clock,
            Options.Create(new OrderingOptions()), NullLogger<OrderService>.Instance);

    /// <summary>Builds a Pending order holding stock on one variant. The reservation window is
    /// an hour in the past by default (so the expiry sweep is due); pass a future value to seed
    /// an order the merchant can still confirm.</summary>
    private static async Task<(ListingVariant Variant, Order Order)> SeedReservedOrderAsync(
        ApplicationDbContext db, TestClock clock, int initialQuantity, int reservedQuantity,
        DateTime? reservationExpiresAtUtc = null)
    {
        const string buyerId = "buyer-user";
        const string merchantUserId = "merchant-user";
        const string adminId = "admin-user";

        db.Users.Add(new ApplicationUser { Id = buyerId, UserName = "buyer@example.test", CreatedAtUtc = clock.UtcNow });
        db.Users.Add(new ApplicationUser { Id = merchantUserId, UserName = "merchant@example.test", CreatedAtUtc = clock.UtcNow });

        var category = new Category("Small Kitchen Appliances", $"category-{Guid.NewGuid():N}", null, 0);
        var grade = new ConditionGrade("A", "Sealed", "New and unopened in the original box.", 1);
        var reason = new DiscountReason("Overstock", "Overstock");
        db.Categories.Add(category);
        db.ConditionGrades.Add(grade);
        db.DiscountReasons.Add(reason);

        var profile = new MerchantProfile(merchantUserId, "Test Merchant", $"slug-{Guid.NewGuid():N}", clock.UtcNow);
        profile.AddDocument(
            MerchantVerificationDocumentType.CommercialRegistration, "key", "doc.pdf", "application/pdf", 10, clock.UtcNow);
        profile.SubmitForReview(clock.UtcNow);
        profile.Approve(adminId, clock.UtcNow);
        db.MerchantProfiles.Add(profile);

        var listing = new Listing(
            profile.Id, category.Id, grade.Id, "Test Kettle", $"test-kettle-{Guid.NewGuid():N}", "A test description.", clock.UtcNow);
        var variant = listing.AddVariant("SKU-KETTLE", [], initialQuantity, clock.UtcNow);
        listing.AddMedia(ListingMediaType.Product, "key-kettle", "photo.jpg", "image/jpeg", 100, null, clock.UtcNow);
        listing.UpdateDetails(
            category.Id, grade.Id, "Test Kettle", "A test description.", referencePrice: null, retailPrice: 25m,
            returnPolicyText: null, warrantyType: WarrantyType.None, warrantyMonths: null, includedItemsText: null,
            missingItemsText: null, discountReasonIds: [reason.Id], clock.UtcNow);
        listing.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        listing.Approve(adminId, "ok", clock.UtcNow);
        db.Listings.Add(listing);

        // Mirrors what checkout does at placement time: reserve the stock, then record the
        // order line against it.
        variant.Reserve(reservedQuantity, clock.UtcNow);

        var order = new Order(
            buyerId, profile.Id, OrderFulfillmentType.Pickup, Guid.NewGuid(), "Pickup from Test Merchant",
            deliveryAddressText: null, contactName: "Test Buyer", contactPhone: "+962790000000", buyerNote: null,
            reservationExpiresAtUtc: reservationExpiresAtUtc ?? clock.UtcNow.AddHours(-1), clock.UtcNow);
        order.AddItem(listing.Id, variant.Id, reservedQuantity, 25m, "Test Kettle", "SKU-KETTLE", "A", "Overstock");
        db.Orders.Add(order);

        await db.SaveChangesAsync();
        return (variant, order);
    }

    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; set; } = now;
    }

    /// <summary>Forces the first <c>SaveChangesAsync</c> to fail as if another request had already changed the row.</summary>
    private sealed class ThrowOnceOnSaveDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public bool ThrowNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowNextSave)
            {
                ThrowNextSave = false;
                throw new DbUpdateConcurrencyException("Simulated: another request already changed this order.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class AllowAllUserRoleService : IUserRoleService
    {
        public Task AddToRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveFromRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class ThrowingMarketplaceService : IPublicMarketplaceService
    {
        public Task<HomePageView> GetHomePageAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ShopResultView> BrowseListingsAsync(ShopQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublicListingDetailView?> GetListingBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublicMerchantProfileView?> GetMerchantStoreHeaderBySlugAsync(
            string slug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
