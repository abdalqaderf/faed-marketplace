using Faed.Web.Models.Enums;

namespace Faed.Web.Services.B2B;

// ---- Inputs ------------------------------------------------------------------------

/// <summary>
/// How the accepting merchant wants an accepted offer fulfilled. The only thing chosen at
/// acceptance is direct pickup vs seller-arranged shipping — the agreed unit price and
/// quantities come from the accepted revision, and the deal total is derived from those alone.
/// Shipping information —
/// the reference and any cost — is seller-owned and is recorded later, by the selling
/// merchant, through the deal's fulfilment steps; acceptance
/// cannot add an unagreed shipping charge or attach a shipment reference.
/// </summary>
public sealed record AcceptOfferInput(B2BFulfillmentType FulfillmentType);

/// <summary>Which deals a merchant's queue should return.</summary>
public enum B2BDealFilter
{
    /// <summary>Everything not yet in a terminal state.</summary>
    Active = 0,

    /// <summary>Reserved, waiting for the selling merchant to start fulfilment.</summary>
    AwaitingFulfillment = 1,

    /// <summary>Ready for pickup, shipped or delivered.</summary>
    InFulfillment = 2,

    Completed = 3,

    Cancelled = 4,

    All = 5,
}

// ---- Views -----------------------------------------------------------------------

/// <summary>A row in a merchant's deal list.</summary>
public sealed record B2BDealSummaryView(
    Guid Id,
    B2BNegotiationParty MyRole,
    B2BDealStatus Status,
    B2BFulfillmentType FulfillmentType,
    string ListingTitle,
    string CounterpartyName,
    int TotalUnits,
    decimal Total,
    DateTime? ReservationExpiresAtUtc,
    DateTime UpdatedAtUtc);

public sealed record B2BDealLineView(
    string VariantCombination,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>The full picture of one deal for one of its two participating merchants.</summary>
public sealed record B2BDealDetailView(
    Guid Id,
    Guid NegotiationId,
    B2BNegotiationParty MyRole,
    B2BDealStatus Status,
    string? StatusReason,
    B2BFulfillmentType FulfillmentType,
    string? ShipmentReference,
    string ListingTitle,
    string ListingSlug,
    string SellingMerchantName,
    string BuyingMerchantName,
    string CounterpartyName,
    decimal AcceptedUnitPrice,
    decimal? ShippingCost,
    decimal Subtotal,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime? ReservationExpiresAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyList<B2BDealLineView> Lines)
{
    public bool IsSeller => MyRole == B2BNegotiationParty.SellingMerchant;

    public bool IsBuyer => MyRole == B2BNegotiationParty.BuyingMerchant;

    public bool SellerCanMarkReadyForPickup =>
        IsSeller && Status == B2BDealStatus.AwaitingFulfillment && FulfillmentType == B2BFulfillmentType.Pickup;

    public bool SellerCanMarkShipped =>
        IsSeller && Status == B2BDealStatus.AwaitingFulfillment
        && FulfillmentType == B2BFulfillmentType.SellerArrangedShipping;

    public bool SellerCanSetShipmentReference =>
        IsSeller && !IsTerminal && FulfillmentType == B2BFulfillmentType.SellerArrangedShipping;

    public bool CanMarkDelivered =>
        Status is B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped;

    /// <summary>Either participant can record the deal as complete once it has been delivered.</summary>
    public bool CanComplete => Status == B2BDealStatus.Delivered;

    /// <summary>Either participant can withdraw before the deal is delivered.</summary>
    public bool CanCancel => Status is B2BDealStatus.AwaitingFulfillment
        or B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped;

    public bool IsTerminal => Status is B2BDealStatus.Completed or B2BDealStatus.Cancelled;
}
