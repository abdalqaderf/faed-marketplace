using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// B2C order aggregate rules (tasks/TASK-006-B2C-ORDERS.md, docs/03-BUSINESS-RULES.md §7-8,
/// docs/05-USER-FLOWS-AND-STATE-MACHINES.md §4, docs/17-DATA-INVARIANTS.md "B2C Order").
/// </summary>
public class OrderTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static Order NewPickupOrder(decimal deliveryFee = 0m)
    {
        var order = new Order(
            buyerUserId: "buyer-1",
            merchantProfileId: Guid.NewGuid(),
            fulfillmentType: OrderFulfillmentType.Pickup,
            merchantLocationId: Guid.NewGuid(),
            deliveryZoneId: null,
            deliveryFeeSnapshot: deliveryFee,
            fulfillmentSnapshot: "Main store — 1 King St, Abdali, Amman",
            deliveryAddressText: null,
            contactName: "Buyer One",
            contactPhone: "0790000000",
            buyerNote: null,
            reservationExpiresAtUtc: Now.AddHours(1),
            nowUtc: Now);
        return order;
    }

    private static Order NewOrderWithItems(int qty = 2, decimal unitPrice = 10m, decimal deliveryFee = 0m)
    {
        var order = NewPickupOrder(deliveryFee);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), qty, unitPrice, "Sneakers", "Size: M", "Grade A — New", "Overstock");
        return order;
    }

    [Fact]
    public void NewOrder_StartsPending_AndComputesTotalsFromLinesPlusFee()
    {
        var order = NewOrderWithItems(qty: 3, unitPrice: 12.500m, deliveryFee: 2.000m);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(37.500m, order.Subtotal);
        Assert.Equal(39.500m, order.Total);
        Assert.Equal(3, order.TotalUnits);
        Assert.True(order.HoldsReservation);
    }

    [Fact]
    public void DeliveryOrder_WithoutAddress_IsRejected()
    {
        Assert.Throws<DomainException>(() => new Order(
            "buyer-1", Guid.NewGuid(), OrderFulfillmentType.MerchantDelivery,
            merchantLocationId: null, deliveryZoneId: Guid.NewGuid(), deliveryFeeSnapshot: 1.5m,
            fulfillmentSnapshot: "Zone A", deliveryAddressText: null,
            contactName: "Buyer", contactPhone: "079", buyerNote: null,
            reservationExpiresAtUtc: Now.AddHours(1), nowUtc: Now));
    }

    [Fact]
    public void AddItem_DuplicateVariant_IsRejected()
    {
        var order = NewPickupOrder();
        var variantId = Guid.NewGuid();
        order.AddItem(Guid.NewGuid(), variantId, 1, 5m, "A", "Size: M", "Grade A", null);

        Assert.Throws<DomainException>(() =>
            order.AddItem(Guid.NewGuid(), variantId, 1, 5m, "A", "Size: M", "Grade A", null));
    }

    [Fact]
    public void Confirm_ClearsReservationExpiry_AndOnlyWorksFromPending()
    {
        var order = NewOrderWithItems();

        order.Confirm(Now.AddMinutes(5));
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.ReservationExpiresAtUtc);
        Assert.NotNull(order.ConfirmedAtUtc);

        Assert.Throws<DomainException>(() => order.Confirm(Now.AddMinutes(10)));
    }

    [Fact]
    public void Confirm_WhenTheReservationHasAlreadyExpired_IsRejected_AndTheOrderStaysPending()
    {
        var order = NewOrderWithItems(); // reservation expires at Now + 1h

        var ex = Assert.Throws<DomainException>(() => order.Confirm(Now.AddHours(2)));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.NotNull(order.ReservationExpiresAtUtc);

        // The expiry sweep can still cancel it and release the hold.
        order.Cancel("The reservation expired before the merchant confirmed the order.", Now.AddHours(2));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.HoldsReservation);
    }

    [Fact]
    public void Confirm_ExactlyAtTheExpiryInstant_IsRejected()
    {
        var order = NewOrderWithItems();
        Assert.Throws<DomainException>(() => order.Confirm(Now.AddHours(1)));
    }

    [Fact]
    public void NewOrder_WithAnOverLongFulfilmentSnapshot_TruncatesRatherThanThrowing()
    {
        var longSnapshot = new string('x', Order.MaxFulfillmentSnapshotLength + 500);

        var order = new Order(
            "buyer-1", Guid.NewGuid(), OrderFulfillmentType.Pickup, Guid.NewGuid(), null, 0m,
            longSnapshot, null, "Buyer One", "0790000000", null, Now.AddHours(1), Now);

        Assert.Equal(Order.MaxFulfillmentSnapshotLength, order.FulfillmentSnapshot.Length);
    }

    [Fact]
    public void NewOrder_WithAnEmptyFulfilmentSnapshot_IsStillRejected()
    {
        Assert.Throws<DomainException>(() => new Order(
            "buyer-1", Guid.NewGuid(), OrderFulfillmentType.Pickup, Guid.NewGuid(), null, 0m,
            "   ", null, "Buyer One", "0790000000", null, Now.AddHours(1), Now));
    }

    [Fact]
    public void PickupHappyPath_RunsPendingToCompleted()
    {
        var order = NewOrderWithItems();

        order.Confirm(Now.AddMinutes(1));
        order.MarkReadyForPickup(Now.AddMinutes(2));
        order.Complete(Now.AddMinutes(3));

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAtUtc);
        Assert.False(order.HoldsReservation);
        Assert.True(order.IsTerminal);
    }

    [Fact]
    public void MarkReadyForPickup_OnADeliveryOrder_IsRejected()
    {
        var order = new Order(
            "buyer-1", Guid.NewGuid(), OrderFulfillmentType.MerchantDelivery,
            null, Guid.NewGuid(), 1.5m, "Zone A", "12 Rainbow St, Amman",
            "Buyer", "079", null, Now.AddHours(1), Now);
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, 5m, "A", "Size: M", "Grade A", null);
        order.Confirm(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => order.MarkReadyForPickup(Now.AddMinutes(2)));
        order.MarkOutForDelivery(Now.AddMinutes(2));
        Assert.Equal(OrderStatus.OutForDelivery, order.Status);
    }

    [Fact]
    public void Complete_BeforeFulfillmentStarts_IsRejected()
    {
        var order = NewOrderWithItems();
        Assert.Throws<DomainException>(() => order.Complete(Now.AddMinutes(1)));

        order.Confirm(Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => order.Complete(Now.AddMinutes(2)));
    }

    [Fact]
    public void Cancel_IsRejectedOnceTerminal()
    {
        var order = NewOrderWithItems();
        order.Confirm(Now.AddMinutes(1));
        order.MarkReadyForPickup(Now.AddMinutes(2));
        order.Complete(Now.AddMinutes(3));

        Assert.Throws<DomainException>(() => order.Cancel("changed my mind", Now.AddMinutes(4)));
    }

    [Fact]
    public void BuyerCanCancel_OnlyWhilePendingOrConfirmed()
    {
        var order = NewOrderWithItems();
        Assert.True(order.BuyerCanCancel);

        order.Confirm(Now.AddMinutes(1));
        Assert.True(order.BuyerCanCancel);

        order.MarkReadyForPickup(Now.AddMinutes(2));
        Assert.False(order.BuyerCanCancel);
        Assert.True(order.MerchantCanCancel);
    }

    [Fact]
    public void MarkNoShow_OnlyFromAFulfillingState_AndReleasesTheHold()
    {
        var order = NewOrderWithItems();
        order.Confirm(Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => order.MarkNoShow("no answer", Now.AddMinutes(2)));

        order.MarkReadyForPickup(Now.AddMinutes(2));
        order.MarkNoShow("buyer never collected", Now.AddMinutes(3));

        Assert.Equal(OrderStatus.NoShow, order.Status);
        Assert.Equal("buyer never collected", order.StatusReason);
        Assert.False(order.HoldsReservation);
    }
}
