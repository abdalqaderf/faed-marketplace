using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Ordering;

// ---- Inputs ------------------------------------------------------------------------

/// <summary>One requested variant line. The price is never supplied by the caller — it is
/// loaded from the listing server-side.</summary>
public sealed record OrderLineInput(Guid VariantId, int Quantity);

/// <summary>
/// Everything needed to place a B2C order. The selling merchant is resolved from the
/// requested variants, never trusted from input; all money is computed server-side
/// </summary>
public sealed record PlaceOrderInput(
    IReadOnlyList<OrderLineInput> Lines,
    OrderFulfillmentType FulfillmentType,
    Guid? MerchantLocationId,
    Guid? DeliveryZoneId,
    string? DeliveryAddressText,
    string ContactName,
    string ContactPhone,
    string? BuyerNote);

// ---- Checkout view ----------------------------------------------------------------

public sealed record CheckoutLineView(
    Guid VariantId,
    string Combination,
    decimal UnitPrice,
    int AvailableQuantity)
{
    public bool IsSellable => AvailableQuantity > 0;
}

public sealed record PickupLocationOption(
    Guid Id, string Name, string Address, string? Instructions, string? Hours);

public sealed record DeliveryZoneOption(
    Guid Id, string Name, decimal DeliveryFee, decimal? MinimumOrderValue, string? Estimate);

/// <summary>The single-listing order builder shown to a signed-in buyer before checkout.</summary>
public sealed record CheckoutView(
    Guid ListingId,
    string ListingTitle,
    string ListingSlug,
    Guid MerchantProfileId,
    string MerchantBusinessName,
    string MerchantSlug,
    string ConditionLabel,
    IReadOnlyList<string> DiscountReasonNames,
    IReadOnlyList<CheckoutLineView> Lines,
    IReadOnlyList<PickupLocationOption> PickupLocations,
    IReadOnlyList<DeliveryZoneOption> DeliveryZones)
{
    public bool CanPickup => PickupLocations.Count > 0;

    public bool CanDeliver => DeliveryZones.Count > 0;

    public bool CanOrder => Lines.Any(l => l.IsSellable) && (CanPickup || CanDeliver);
}

// ---- Order views ----------------------------------------------------------------

/// <summary>A row in a buyer's order history or a merchant's order queue.</summary>
public sealed record OrderSummaryView(
    Guid Id,
    OrderStatus Status,
    OrderFulfillmentType FulfillmentType,
    string Counterparty,
    int TotalUnits,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime? ReservationExpiresAtUtc);

public sealed record OrderLineView(
    string ListingTitle,
    string? ListingSlug,
    string VariantSnapshot,
    string ConditionSnapshot,
    string? DiscountReasonSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>The full picture of one order for its buyer or its selling merchant.</summary>
public sealed record OrderDetailView(
    Guid Id,
    OrderStatus Status,
    string? StatusReason,
    OrderFulfillmentType FulfillmentType,
    string FulfillmentSnapshot,
    string? DeliveryAddressText,
    decimal DeliveryFeeSnapshot,
    decimal Subtotal,
    decimal Total,
    string ContactName,
    string ContactPhone,
    string? BuyerNote,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? ReservationExpiresAtUtc,
    Guid MerchantProfileId,
    string MerchantBusinessName,
    string MerchantSlug,
    IReadOnlyList<OrderLineView> Items)
{
    public bool BuyerCanCancel => Status is OrderStatus.Pending or OrderStatus.Confirmed;

    /// <summary>The buyer can confirm receipt once the merchant has handed the order over.</summary>
    public bool BuyerCanConfirmReceipt =>
        Status is OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;

    public bool MerchantCanConfirm => Status == OrderStatus.Pending;

    public bool MerchantCanMarkReadyForPickup =>
        Status == OrderStatus.Confirmed && FulfillmentType == OrderFulfillmentType.Pickup;

    public bool MerchantCanMarkOutForDelivery =>
        Status == OrderStatus.Confirmed && FulfillmentType == OrderFulfillmentType.MerchantDelivery;

    public bool MerchantCanComplete =>
        Status is OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;

    public bool MerchantCanMarkNoShow =>
        Status is OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;

    public bool MerchantCanCancel => Status is OrderStatus.Pending or OrderStatus.Confirmed
        or OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;
}

/// <summary>Which orders a merchant's queue should return.</summary>
public enum MerchantOrderFilter
{
    /// <summary>Everything not yet in a terminal state.</summary>
    Open = 0,

    /// <summary>Placed, waiting for the merchant to confirm.</summary>
    NeedsConfirmation = 1,

    /// <summary>Confirmed and in fulfilment.</summary>
    InFulfillment = 2,

    Completed = 3,

    Cancelled = 4,

    All = 5,
}
