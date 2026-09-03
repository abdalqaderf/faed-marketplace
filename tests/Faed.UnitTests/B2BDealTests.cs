using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// Accepted B2B deal aggregate rules (tasks/TASK-008-B2B-DEALS.md,
/// docs/03-BUSINESS-RULES.md §10, docs/05-USER-FLOWS-AND-STATE-MACHINES.md §7,
/// docs/17-DATA-INVARIANTS.md "B2B Deal"). The aggregate owns the fulfilment state machine;
/// the stock movements themselves are the deal service's job.
/// </summary>
public class B2BDealTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid Buyer = Guid.NewGuid();
    private static readonly Guid Negotiation = Guid.NewGuid();
    private static readonly Guid Revision = Guid.NewGuid();
    private static readonly Guid VariantA = Guid.NewGuid();
    private static readonly Guid VariantB = Guid.NewGuid();

    private static B2BDeal NewDeal(
        B2BFulfillmentType fulfillmentType = B2BFulfillmentType.Pickup,
        decimal? shippingCost = null,
        DateTime? reservationExpiresAtUtc = null)
    {
        var deal = new B2BDeal(
            Negotiation, Revision, Seller, Buyer, fulfillmentType, shipmentReference: null,
            acceptedUnitPriceSnapshot: 5m, shippingCostSnapshot: shippingCost,
            subtotalSnapshot: 50m,
            reservationExpiresAtUtc: reservationExpiresAtUtc ?? Now.AddDays(7), nowUtc: Now);
        deal.AddLine(VariantA, 10, 5m, "Size: M");
        return deal;
    }

    [Fact]
    public void NewDeal_StartsAwaitingFulfillment_WithLinesAndAReservationWindow()
    {
        var deal = NewDeal();

        Assert.Equal(B2BDealStatus.AwaitingFulfillment, deal.Status);
        Assert.Equal(10, deal.TotalUnits);
        Assert.Equal(50m, deal.Lines.Single().LineTotalSnapshot);
        Assert.Equal(Now.AddDays(7), deal.ReservationExpiresAtUtc);
        Assert.True(deal.IsParticipant(Seller));
        Assert.True(deal.IsParticipant(Buyer));
        Assert.False(deal.IsParticipant(Guid.NewGuid()));
    }

    [Fact]
    public void Ctor_WhenTheTwoMerchantsAreTheSame_IsRejected()
    {
        Assert.Throws<DomainException>(() => new B2BDeal(
            Negotiation, Revision, Seller, Seller, B2BFulfillmentType.Pickup, null,
            5m, null, 50m, Now.AddDays(7), Now));
    }

    [Fact]
    public void Ctor_DerivesTheTotalFromTheSubtotalPlusAnyShippingCost_NotFromACallerSuppliedTotal()
    {
        var noShipping = new B2BDeal(
            Negotiation, Revision, Seller, Buyer, B2BFulfillmentType.Pickup, null,
            5m, null, 50m, Now.AddDays(7), Now);
        Assert.Equal(50m, noShipping.TotalSnapshot);
        Assert.Null(noShipping.ShippingCostSnapshot);

        var withShipping = new B2BDeal(
            Negotiation, Revision, Seller, Buyer, B2BFulfillmentType.SellerArrangedShipping, null,
            5m, 3.500m, 50m, Now.AddDays(7), Now);
        Assert.Equal(53.500m, withShipping.TotalSnapshot);
    }

    [Fact]
    public void Ctor_Pickup_WithAShipmentReferenceOrAShippingCost_IsRejected()
    {
        Assert.Throws<DomainException>(() => new B2BDeal(
            Negotiation, Revision, Seller, Buyer, B2BFulfillmentType.Pickup, "WAYBILL-1",
            5m, null, 50m, Now.AddDays(7), Now));

        Assert.Throws<DomainException>(() => new B2BDeal(
            Negotiation, Revision, Seller, Buyer, B2BFulfillmentType.Pickup, null,
            5m, 2.000m, 50m, Now.AddDays(7), Now));
    }

    [Fact]
    public void AddLine_TwiceForTheSameVariant_IsRejected()
    {
        var deal = NewDeal();
        Assert.Throws<DomainException>(() => deal.AddLine(VariantA, 3, 5m, "Size: M"));
    }

    [Fact]
    public void Pickup_HappyPath_RunsThroughToCompleted()
    {
        var deal = NewDeal(B2BFulfillmentType.Pickup);

        deal.MarkReadyForPickup(Now.AddHours(1));
        Assert.Equal(B2BDealStatus.ReadyForPickup, deal.Status);
        // Fulfilment started — the reservation no longer lapses on its own.
        Assert.Null(deal.ReservationExpiresAtUtc);

        deal.MarkDelivered(Now.AddHours(2));
        Assert.Equal(B2BDealStatus.Delivered, deal.Status);

        deal.Complete(Now.AddHours(3));
        Assert.Equal(B2BDealStatus.Completed, deal.Status);
        Assert.Equal(Now.AddHours(3), deal.CompletedAtUtc);
    }

    [Fact]
    public void Shipping_HappyPath_RecordsTheReferenceAndCompletes()
    {
        var deal = NewDeal(B2BFulfillmentType.SellerArrangedShipping);

        deal.MarkShipped("WAYBILL-123", Now.AddHours(1));
        Assert.Equal(B2BDealStatus.Shipped, deal.Status);
        Assert.Equal("WAYBILL-123", deal.ShipmentReference);
        Assert.Null(deal.ReservationExpiresAtUtc);

        deal.MarkDelivered(Now.AddHours(2));
        deal.Complete(Now.AddHours(3));
        Assert.Equal(B2BDealStatus.Completed, deal.Status);
    }

    [Fact]
    public void MarkReadyForPickup_OnAShippingDeal_IsRejected()
    {
        var deal = NewDeal(B2BFulfillmentType.SellerArrangedShipping);
        Assert.Throws<DomainException>(() => deal.MarkReadyForPickup(Now.AddHours(1)));
    }

    [Fact]
    public void MarkShipped_OnAPickupDeal_IsRejected()
    {
        var deal = NewDeal(B2BFulfillmentType.Pickup);
        Assert.Throws<DomainException>(() => deal.MarkShipped(null, Now.AddHours(1)));
    }

    [Fact]
    public void SetShipmentReference_OnlyAppliesToShippingDeals_AndRequiresAValue()
    {
        var pickup = NewDeal(B2BFulfillmentType.Pickup);
        Assert.Throws<DomainException>(() => pickup.SetShipmentReference("X", Now.AddHours(1)));

        var shipping = NewDeal(B2BFulfillmentType.SellerArrangedShipping);
        Assert.Throws<DomainException>(() => shipping.SetShipmentReference(" ", Now.AddHours(1)));
        shipping.SetShipmentReference("REF-9", Now.AddHours(1));
        Assert.Equal("REF-9", shipping.ShipmentReference);
    }

    [Fact]
    public void Complete_BeforeDelivery_IsRejected()
    {
        var deal = NewDeal();
        Assert.Throws<DomainException>(() => deal.Complete(Now.AddHours(1)));

        deal.MarkReadyForPickup(Now.AddHours(1));
        Assert.Throws<DomainException>(() => deal.Complete(Now.AddHours(2)));
    }

    [Fact]
    public void Cancel_IsAllowedBeforeDelivery_ButNotAfterItOrOnceTerminal()
    {
        var deal = NewDeal();
        deal.MarkReadyForPickup(Now.AddHours(1));
        deal.MarkDelivered(Now.AddHours(2));

        Assert.Throws<DomainException>(() => deal.Cancel("too late", Now.AddHours(3)));

        deal.Complete(Now.AddHours(4));
        Assert.Throws<DomainException>(() => deal.Cancel("way too late", Now.AddHours(5)));
    }

    [Fact]
    public void Cancel_FromAwaitingFulfillment_ClearsTheReservationWindow_AndRecordsTheReason()
    {
        var deal = NewDeal();

        deal.Cancel("Buyer backed out", Now.AddHours(1));

        Assert.Equal(B2BDealStatus.Cancelled, deal.Status);
        Assert.Equal("Buyer backed out", deal.StatusReason);
        Assert.Equal(Now.AddHours(1), deal.CancelledAtUtc);
        Assert.Null(deal.ReservationExpiresAtUtc);
    }

    [Fact]
    public void Total_IncludesTheShippingCostSnapshot()
    {
        var deal = NewDeal(B2BFulfillmentType.SellerArrangedShipping, shippingCost: 4.500m);
        Assert.Equal(54.500m, deal.TotalSnapshot);
    }

    [Fact]
    public void MarkReadyForPickup_WhenTheReservationHasAlreadyExpired_IsRejected_AndTheDealStaysAwaitingFulfillment()
    {
        var deal = NewDeal(B2BFulfillmentType.Pickup, reservationExpiresAtUtc: Now.AddHours(1));

        var ex = Assert.Throws<DomainException>(() => deal.MarkReadyForPickup(Now.AddHours(2)));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(B2BDealStatus.AwaitingFulfillment, deal.Status);
        Assert.NotNull(deal.ReservationExpiresAtUtc);
    }

    [Fact]
    public void MarkShipped_WhenTheReservationHasAlreadyExpired_IsRejected_AndTheDealStaysAwaitingFulfillment()
    {
        var deal = NewDeal(B2BFulfillmentType.SellerArrangedShipping, reservationExpiresAtUtc: Now.AddHours(1));

        Assert.Throws<DomainException>(() => deal.MarkShipped("WB-1", Now.AddHours(2)));
        Assert.Equal(B2BDealStatus.AwaitingFulfillment, deal.Status);
        Assert.Null(deal.ShipmentReference);
    }
}
