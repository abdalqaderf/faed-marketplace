using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// An accepted merchant-to-merchant deal: the fulfillment record created when a
/// <see cref="B2BNegotiation"/> participant accepts the current offer revision
/// (docs/03-BUSINESS-RULES.md §10, docs/04-DOMAIN-MODEL.md §8,
/// docs/adr/0004-B2B-NEGOTIATION-SEPARATE-FROM-DEAL.md). It is distinct from the negotiation:
/// the accepted terms are snapshotted here and never read back from the mutable listing, and
/// the deal carries its own <see cref="ReservationExpiresAtUtc"/>, separate from a revision's
/// <c>OfferExpiresAtUtc</c> (AGENTS.md Rule C).
///
/// Like <see cref="Order"/>, this aggregate owns the fulfillment state machine but not the
/// stock movements: it records the transition and its timestamp, and the deal service moves
/// the reserved / available / sold quantities on each <see cref="B2BDealLine"/>'s variant
/// inside the same transaction (docs/06-ARCHITECTURE.md §6). The atomic reservation of every
/// line happens once, when the deal is created (docs/17-DATA-INVARIANTS.md "Inventory for all
/// deal lines reserves atomically or not at all").
/// </summary>
public class B2BDeal
{
    public const int MaxShipmentReferenceLength = 200;
    public const int MaxStatusReasonLength = 500;

    private readonly List<B2BDealLine> _lines = [];

    private B2BDeal()
    {
    }

    /// <summary>
    /// Creates the deal from the terms both merchants actually agreed on. The subtotal is the
    /// accepted revision's server-derived total (accepted unit price × agreed quantity) and
    /// nothing else; the deal's <see cref="TotalSnapshot"/> is computed here from the subtotal
    /// plus any agreed <paramref name="shippingCostSnapshot"/> — a caller cannot inject a
    /// standalone total (docs/08-SECURITY-AND-PRIVACY.md §7, docs/17-DATA-INVARIANTS.md
    /// "Order/deal total = server-calculated line totals + eligible fulfillment/shipping
    /// snapshot"). A direct-pickup deal carries no shipment reference and no shipping cost:
    /// shipping information belongs only to a seller-arranged-shipping deal
    /// (docs/03-BUSINESS-RULES.md §12).
    /// </summary>
    public B2BDeal(
        Guid b2bNegotiationId,
        Guid acceptedRevisionId,
        Guid sellingMerchantProfileId,
        Guid buyingMerchantProfileId,
        B2BFulfillmentType fulfillmentType,
        string? shipmentReference,
        decimal acceptedUnitPriceSnapshot,
        decimal? shippingCostSnapshot,
        decimal subtotalSnapshot,
        DateTime reservationExpiresAtUtc,
        DateTime nowUtc)
    {
        if (sellingMerchantProfileId == buyingMerchantProfileId)
        {
            throw new DomainException("A wholesale deal needs two different merchants.");
        }

        if (!Enum.IsDefined(fulfillmentType))
        {
            throw new DomainException("Choose how the deal will be fulfilled.");
        }

        if (acceptedUnitPriceSnapshot < 0 || subtotalSnapshot < 0)
        {
            throw new DomainException("A deal's monetary snapshots cannot be negative.");
        }

        if (shippingCostSnapshot is < 0)
        {
            throw new DomainException("A shipping cost cannot be negative.");
        }

        var normalizedReference = NormalizeReference(shipmentReference);
        if (fulfillmentType == B2BFulfillmentType.Pickup && (normalizedReference is not null || shippingCostSnapshot is not null))
        {
            // A pickup deal with a shipment reference or a shipping charge is contradictory
            // fulfilment data (docs/03-BUSINESS-RULES.md §12 — shipping information belongs to
            // seller-arranged shipping only).
            throw new DomainException("A direct-pickup deal cannot carry a shipment reference or a shipping cost.");
        }

        Id = Guid.CreateVersion7();
        B2BNegotiationId = b2bNegotiationId;
        AcceptedRevisionId = acceptedRevisionId;
        SellingMerchantProfileId = sellingMerchantProfileId;
        BuyingMerchantProfileId = buyingMerchantProfileId;
        Status = B2BDealStatus.AwaitingFulfillment;
        FulfillmentType = fulfillmentType;
        ShipmentReference = normalizedReference;
        AcceptedUnitPriceSnapshot = acceptedUnitPriceSnapshot;
        ShippingCostSnapshot = shippingCostSnapshot;
        SubtotalSnapshot = subtotalSnapshot;
        TotalSnapshot = subtotalSnapshot + (shippingCostSnapshot ?? 0m);
        ReservationExpiresAtUtc = reservationExpiresAtUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid B2BNegotiationId { get; private set; }

    /// <summary>The revision both merchants agreed on. Every deal line corresponds to it (docs/17-DATA-INVARIANTS.md).</summary>
    public Guid AcceptedRevisionId { get; private set; }

    public Guid SellingMerchantProfileId { get; private set; }

    public Guid BuyingMerchantProfileId { get; private set; }

    public B2BDealStatus Status { get; private set; }

    public B2BFulfillmentType FulfillmentType { get; private set; }

    /// <summary>Seller-entered shipment reference. Faed does not book or price shipping (docs/03-BUSINESS-RULES.md §12).</summary>
    public string? ShipmentReference { get; private set; }

    public decimal AcceptedUnitPriceSnapshot { get; private set; }

    public decimal? ShippingCostSnapshot { get; private set; }

    public decimal SubtotalSnapshot { get; private set; }

    public decimal TotalSnapshot { get; private set; }

    /// <summary>
    /// When the stock reservation lapses while the deal is still
    /// <see cref="B2BDealStatus.AwaitingFulfillment"/> (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §7).
    /// Cleared once the seller starts fulfilling it — the stock is then held until the deal is
    /// delivered or cancelled. The window is configuration, not a domain constant
    /// (docs/13-OPEN-QUESTIONS.md §15).
    /// </summary>
    public DateTime? ReservationExpiresAtUtc { get; private set; }

    public string? StatusReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Guards the two merchants acting on the same deal at the same time.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<B2BDealLine> Lines => _lines.AsReadOnly();

    public int TotalUnits => _lines.Sum(l => l.Quantity);

    /// <summary>True while the deal still holds reserved stock a later transition must release or consume.</summary>
    public bool HoldsReservation => Status is B2BDealStatus.AwaitingFulfillment
        or B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped or B2BDealStatus.Delivered;

    public bool IsTerminal => Status is B2BDealStatus.Completed or B2BDealStatus.Cancelled;

    public bool IsParticipant(Guid merchantProfileId) =>
        merchantProfileId == SellingMerchantProfileId || merchantProfileId == BuyingMerchantProfileId;

    // ---- Building -----------------------------------------------------------------

    /// <summary>Adds a deal line from the accepted revision. Only valid while the deal is being created.</summary>
    public B2BDealLine AddLine(Guid listingVariantId, int quantity, decimal unitPriceSnapshot, string variantSnapshot)
    {
        if (Status != B2BDealStatus.AwaitingFulfillment)
        {
            throw new DomainException("Lines cannot be added to a deal after it has been created.");
        }

        if (_lines.Any(l => l.ListingVariantId == listingVariantId))
        {
            throw new DomainException("That variant is already on this deal.");
        }

        var line = new B2BDealLine(listingVariantId, quantity, unitPriceSnapshot, variantSnapshot);
        _lines.Add(line);
        return line;
    }

    // ---- Lifecycle ---------------------------------------------------------------

    /// <summary>Seller marks a direct-pickup deal ready for the buying merchant to collect.</summary>
    public void MarkReadyForPickup(DateTime nowUtc)
    {
        RequireStatus(B2BDealStatus.AwaitingFulfillment, "marked ready for pickup");
        RequireReservationNotExpired(nowUtc);
        if (FulfillmentType != B2BFulfillmentType.Pickup)
        {
            throw new DomainException("Only a direct-pickup deal can be marked ready for pickup.");
        }

        Status = B2BDealStatus.ReadyForPickup;
        // Fulfilment has started; the reservation no longer lapses on its own.
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    /// <summary>Seller marks a seller-arranged-shipping deal as dispatched, optionally recording a shipment reference.</summary>
    public void MarkShipped(string? shipmentReference, DateTime nowUtc)
    {
        RequireStatus(B2BDealStatus.AwaitingFulfillment, "marked shipped");
        RequireReservationNotExpired(nowUtc);
        if (FulfillmentType != B2BFulfillmentType.SellerArrangedShipping)
        {
            throw new DomainException("Only a seller-arranged-shipping deal can be marked shipped.");
        }

        var normalized = NormalizeReference(shipmentReference);
        if (normalized is not null)
        {
            ShipmentReference = normalized;
        }

        Status = B2BDealStatus.Shipped;
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    /// <summary>Seller records or updates the shipment reference for a seller-arranged-shipping deal.</summary>
    public void SetShipmentReference(string shipmentReference, DateTime nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainException($"A deal in status {Status} can no longer be updated.");
        }

        if (FulfillmentType != B2BFulfillmentType.SellerArrangedShipping)
        {
            throw new DomainException("A shipment reference only applies to a seller-arranged-shipping deal.");
        }

        ShipmentReference = NormalizeReference(shipmentReference)
            ?? throw new DomainException("Enter a shipment reference.");
        Touch(nowUtc);
    }

    /// <summary>The buying merchant has taken delivery (collected the pickup or received the shipment).</summary>
    public void MarkDelivered(DateTime nowUtc)
    {
        if (Status is not (B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped))
        {
            throw new DomainException($"A deal in status {Status} cannot be marked delivered.");
        }

        Status = B2BDealStatus.Delivered;
        Touch(nowUtc);
    }

    /// <summary>
    /// Fulfilment is finished; the deal service converts the reserved stock to sold in the
    /// same transaction (docs/03-BUSINESS-RULES.md §10 "On completion: Reserved -> Sold").
    /// </summary>
    public void Complete(DateTime nowUtc)
    {
        RequireStatus(B2BDealStatus.Delivered, "completed");

        Status = B2BDealStatus.Completed;
        CompletedAtUtc = nowUtc;
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    /// <summary>
    /// A participant withdraws from the deal before it completes; the deal service releases
    /// the reserved stock in the same transaction (docs/03-BUSINESS-RULES.md §10 "If deal
    /// expires/cancels before stock is consumed: Reserved -> Available").
    /// </summary>
    public void Cancel(string reason, DateTime nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainException($"A deal in status {Status} can no longer be cancelled.");
        }

        if (Status == B2BDealStatus.Delivered)
        {
            throw new DomainException("This deal has been delivered. It can only be completed now.");
        }

        Status = B2BDealStatus.Cancelled;
        StatusReason = Require(reason, "cancellation reason", MaxStatusReasonLength);
        CancelledAtUtc = nowUtc;
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    private void RequireStatus(B2BDealStatus expected, string verb)
    {
        if (Status != expected)
        {
            throw new DomainException($"A deal in status {Status} cannot be {verb}.");
        }
    }

    /// <summary>
    /// A fulfilment step must not advance a deal whose stock reservation has already lapsed:
    /// doing so would clear <see cref="ReservationExpiresAtUtc"/> and hold the stock
    /// indefinitely on the strength of a passed deadline (the same rule as
    /// <see cref="Order.Confirm"/>, docs/03-BUSINESS-RULES.md §10). The deal stays
    /// <see cref="B2BDealStatus.AwaitingFulfillment"/> so the expiry sweep releases it.
    /// </summary>
    private void RequireReservationNotExpired(DateTime nowUtc)
    {
        if (ReservationExpiresAtUtc is { } expiresAt && expiresAt <= nowUtc)
        {
            throw new DomainException(
                "This deal's stock reservation has expired. It will be released — the offer would need to be re-made.");
        }
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    private static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();
        return trimmed.Length > MaxShipmentReferenceLength ? trimmed[..MaxShipmentReferenceLength] : trimmed;
    }

    private static string Require(string value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException($"The {field} is required.");
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
