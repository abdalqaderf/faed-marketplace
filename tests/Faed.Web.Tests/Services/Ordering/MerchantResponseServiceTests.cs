using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Ordering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faed.Web.Tests.Services.Ordering;

/// <summary>
/// The merchant response rate (docs/BUSINESS-MODEL.md §8.4): rolling-30-day
/// confirmed-before-deadline ÷ received, with a bucketed median time. Below five orders in the
/// window a merchant shows "New seller" and never a percentage, and a merchant with a real,
/// poor rate ranks below responsive merchants in the catalogue.
/// </summary>
public class MerchantResponseServiceTests
{
    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Merchant_WithEnoughOrders_ShowsRateAndBucketedTime()
    {
        await using var db = CreateDb();
        var merchantId = Guid.NewGuid();

        // Five received: four confirmed 30 minutes in, one never confirmed.
        for (var i = 0; i < 4; i++)
        {
            db.Orders.Add(Order(merchantId, placedAt: T0.AddDays(-i), confirmAfter: TimeSpan.FromMinutes(30)));
        }
        db.Orders.Add(Order(merchantId, placedAt: T0.AddDays(-5), confirmAfter: null));
        await db.SaveChangesAsync();

        var stats = await NewService(db).GetAsync(merchantId);

        Assert.False(stats.IsNewSeller);
        Assert.Equal(5, stats.OrdersReceived);
        Assert.Equal(80, stats.ResponseRatePercent);
        Assert.Equal(ResponseTimeBucket.WithinHour, stats.MedianResponse);
    }

    [Fact]
    public async Task Merchant_BelowFiveOrders_IsNewSeller_WithNoPercentage()
    {
        await using var db = CreateDb();
        var merchantId = Guid.NewGuid();

        db.Orders.Add(Order(merchantId, T0, TimeSpan.FromMinutes(5)));
        db.Orders.Add(Order(merchantId, T0.AddDays(-1), TimeSpan.FromMinutes(5)));
        db.Orders.Add(Order(merchantId, T0.AddDays(-2), null));
        await db.SaveChangesAsync();

        var stats = await NewService(db).GetAsync(merchantId);

        Assert.True(stats.IsNewSeller);
        Assert.Null(stats.ResponseRatePercent);
    }

    [Fact]
    public async Task LowResponders_AreListed_ButNotNewSellersOrGoodResponders()
    {
        await using var db = CreateDb();
        var good = Guid.NewGuid();
        var poor = Guid.NewGuid();
        var newSeller = Guid.NewGuid();

        for (var i = 0; i < 6; i++)
        {
            db.Orders.Add(Order(good, T0.AddDays(-i), TimeSpan.FromMinutes(20)));
            db.Orders.Add(Order(poor, T0.AddDays(-i), i == 0 ? TimeSpan.FromMinutes(20) : null));
        }
        db.Orders.Add(Order(newSeller, T0, null));
        await db.SaveChangesAsync();

        var low = await NewService(db).GetLowResponderMerchantIdsAsync();

        Assert.Contains(poor, low);
        Assert.DoesNotContain(good, low);
        Assert.DoesNotContain(newSeller, low);
    }

    [Fact]
    public async Task OrdersOlderThanTheWindow_AreIgnored()
    {
        await using var db = CreateDb();
        var merchantId = Guid.NewGuid();

        for (var i = 0; i < 6; i++)
        {
            db.Orders.Add(Order(merchantId, T0.AddDays(-40 - i), null));
        }
        await db.SaveChangesAsync();

        var stats = await NewService(db).GetAsync(merchantId);

        Assert.Equal(0, stats.OrdersReceived);
        Assert.True(stats.IsNewSeller);
    }

    private static MerchantResponseService NewService(ApplicationDbContext db) =>
        new(db, new FixedClock(T0.AddHours(1)), Options.Create(new OrderingOptions()));

    private static Order Order(Guid merchantId, DateTime placedAt, TimeSpan? confirmAfter)
    {
        var order = new Order(
            "buyer", merchantId, OrderFulfillmentType.Pickup, Guid.NewGuid(), "Pickup",
            deliveryAddressText: null, contactName: "Buyer", contactPhone: "+962790000000", buyerNote: null,
            reservationExpiresAtUtc: placedAt.AddHours(12), placedAt);

        if (confirmAfter is { } after)
        {
            order.Confirm(placedAt + after);
        }

        return order;
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
