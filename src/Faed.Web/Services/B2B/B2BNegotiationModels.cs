using Faed.Web.Models.Enums;

namespace Faed.Web.Services.B2B;

// ---- Inputs ------------------------------------------------------------------------

/// <summary>One requested variant line on an offer. The price is never per-line — the whole
/// offer carries a single proposed unit price (docs/04-DOMAIN-MODEL.md §7).</summary>
public sealed record B2BOfferLineInput(Guid VariantId, int Quantity);

/// <summary>
/// A merchant's first offer that opens a negotiation. The selling merchant, the current
/// listing price basis and every monetary total are resolved server-side; nothing here is
/// trusted straight from the request (docs/08-SECURITY-AND-PRIVACY.md §6-7).
/// </summary>
public sealed record StartNegotiationInput(
    string ListingSlug,
    IReadOnlyList<B2BOfferLineInput> Lines,
    decimal ProposedUnitPrice,
    string? Message,
    int? ValidityDays);

/// <summary>A counter-offer within an existing negotiation. Becomes a new immutable revision.</summary>
public sealed record CounterOfferInput(
    IReadOnlyList<B2BOfferLineInput> Lines,
    decimal ProposedUnitPrice,
    string? Message,
    int? ValidityDays);

/// <summary>Which negotiations a merchant's queue should return.</summary>
public enum B2BNegotiationFilter
{
    /// <summary>Open negotiations waiting for this merchant to respond.</summary>
    AwaitingMe = 0,

    /// <summary>Open negotiations waiting for the other merchant.</summary>
    AwaitingThem = 1,

    /// <summary>Every open negotiation this merchant is in.</summary>
    Open = 2,

    /// <summary>Accepted, rejected, expired or cancelled.</summary>
    Closed = 3,

    All = 4,
}

/// <summary>The caller's side of a negotiation.</summary>
public enum B2BNegotiationParty
{
    /// <summary>The caller's merchant owns the listing and receives offers.</summary>
    SellingMerchant = 0,

    /// <summary>The caller's merchant opened the negotiation to buy.</summary>
    BuyingMerchant = 1,
}

// ---- Views -----------------------------------------------------------------------

/// <summary>A variant a merchant can put on an offer, with the stock currently visible.</summary>
public sealed record OfferVariantOption(Guid VariantId, string Combination, int AvailableQuantity);

/// <summary>The listing context for building an offer (the "Make an offer" form).</summary>
public sealed record OfferListingView(
    Guid ListingId,
    string Title,
    string Slug,
    string SellingMerchantName,
    int MinimumOrderQuantity,
    bool AllowMixedVariantLots,
    decimal? IndicativeUnitPrice,
    IReadOnlyList<OfferVariantOption> Variants);

/// <summary>A row in a merchant's negotiation list.</summary>
public sealed record B2BNegotiationSummaryView(
    Guid Id,
    B2BNegotiationParty MyRole,
    B2BNegotiationStatus Status,
    string ListingTitle,
    string ListingSlug,
    string CounterpartyName,
    int CurrentRevisionNumber,
    decimal CurrentUnitPrice,
    int CurrentTotalQuantity,
    decimal CurrentTotal,
    DateTime CurrentOfferExpiresAtUtc,
    bool AwaitingMyResponse,
    DateTime UpdatedAtUtc);

public sealed record B2BOfferLineView(string VariantCombination, int Quantity);

/// <summary>One revision in the negotiation history.</summary>
public sealed record B2BOfferRevisionView(
    int RevisionNumber,
    B2BNegotiationParty ProposedBy,
    string ProposedByName,
    decimal UnitPrice,
    int TotalQuantity,
    decimal Total,
    string? Message,
    DateTime OfferExpiresAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<B2BOfferLineView> Lines);

/// <summary>The full negotiation for one of its two participating merchants.</summary>
public sealed record B2BNegotiationDetailView(
    Guid Id,
    B2BNegotiationParty MyRole,
    B2BNegotiationStatus Status,
    string ListingTitle,
    string ListingSlug,
    string SellingMerchantName,
    string BuyingMerchantName,
    string CounterpartyName,
    int MinimumOrderQuantity,
    bool AllowMixedVariantLots,
    bool CurrentOfferHasExpired,
    bool AwaitingMyResponse,
    IReadOnlyList<B2BOfferRevisionView> Revisions,
    IReadOnlyList<OfferVariantOption> Variants)
{
    public B2BOfferRevisionView CurrentRevision => Revisions[^1];

    /// <summary>True when the caller can accept/reject/counter the current offer right now.</summary>
    public bool CanRespond => Status == B2BNegotiationStatus.Open && AwaitingMyResponse && !CurrentOfferHasExpired;

    /// <summary>Either participant can withdraw from an open negotiation.</summary>
    public bool CanCancel => Status == B2BNegotiationStatus.Open;
}
