using System.Security.Cryptography;
using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A B2C order: exactly one buyer, exactly one selling merchant, one or more variant lines
/// from that merchant. Faed does
/// not run a multi-merchant cart.
/// The aggregate owns the stock-workflow rules but not the stock movements themselves: it
/// records the status transition and its timestamp, and the order service moves the
/// reserved/available/sold quantities on each <see cref="OrderItem"/>'s variant inside the
/// same transaction. Money is always server-calculated here from the line snapshots — never
/// trusted from input. Faed does not model a delivery fee: any delivery arrangement is
/// settled between the buyer and the merchant directly.
/// </summary>
public class Order
{
    public const int MaxContactNameLength = 120;
    public const int MaxContactPhoneLength = 40;
    public const int MaxDeliveryAddressLength = 600;
    public const int MaxBuyerNoteLength = 1000;
    public const int MaxStatusReasonLength = 500;

    // No I, L, O, 0 or 1: this code is read aloud over the phone and typed into WhatsApp, so
    // characters that are easily confused by ear or by eye are excluded by design.
    public const string ReferenceAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int ReferenceLength = 6;

    /// <summary>
    /// Storage ceiling for the human-readable fulfilment description. Sized generously above
    /// the largest string the checkout can build from the maximum-length
    /// <see cref="MerchantLocation"/> fields (name + address + area + city + hours +
    /// instructions ≈ 1.4k); the constructor truncates rather than rejecting, so no valid
    /// merchant data can make checkout fail (regression: a long-but-valid pickup location).
    /// </summary>
    public const int MaxFulfillmentSnapshotLength = 2000;

    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Order(
        string buyerUserId,
        Guid merchantProfileId,
        OrderFulfillmentType fulfillmentType,
        Guid? merchantLocationId,
        string fulfillmentSnapshot,
        string? deliveryAddressText,
        string contactName,
        string contactPhone,
        string? buyerNote,
        DateTime reservationExpiresAtUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(buyerUserId))
        {
            throw new DomainException("An order needs a buyer.");
        }

        if (!Enum.IsDefined(fulfillmentType))
        {
            throw new DomainException("Choose how the order will be fulfilled.");
        }

        if (fulfillmentType == OrderFulfillmentType.Pickup && merchantLocationId is null)
        {
            throw new DomainException("A pickup order needs a pickup location.");
        }

        Id = Guid.CreateVersion7();
        Reference = GenerateReference();
        BuyerUserId = buyerUserId;
        MerchantProfileId = merchantProfileId;
        Status = OrderStatus.Pending;
        FulfillmentType = fulfillmentType;
        MerchantLocationId = merchantLocationId;
        FulfillmentSnapshot = RequireThenTruncate(
            fulfillmentSnapshot, "fulfilment details", MaxFulfillmentSnapshotLength);
        DeliveryAddressText = Optional(deliveryAddressText, "delivery address", MaxDeliveryAddressLength);
        ContactName = Require(contactName, "contact name", MaxContactNameLength);
        ContactPhone = Require(contactPhone, "contact phone", MaxContactPhoneLength);
        BuyerNote = Optional(buyerNote, "note", MaxBuyerNoteLength);
        ReservationExpiresAtUtc = reservationExpiresAtUtc;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        if (fulfillmentType == OrderFulfillmentType.MerchantDelivery && DeliveryAddressText is null)
        {
            throw new DomainException("A delivery order needs a delivery address.");
        }
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Six-character human-readable code a buyer and merchant read aloud to each other over
    /// the phone or type into WhatsApp. Unique, generated once at order creation — never the
    /// authorization key, which stays <see cref="Id"/>.
    /// </summary>
    public string Reference { get; private set; } = null!;

    /// <summary>The Identity user id of the buyer. Their orders are private to them.</summary>
    public string BuyerUserId { get; private set; } = null!;

    public Guid MerchantProfileId { get; private set; }

    public OrderStatus Status { get; private set; }

    public OrderFulfillmentType FulfillmentType { get; private set; }

    public Guid? MerchantLocationId { get; private set; }

    /// <summary>Human-readable fulfilment description captured at checkout (location or delivery details).</summary>
    public string FulfillmentSnapshot { get; private set; } = null!;

    public string? DeliveryAddressText { get; private set; }

    public string ContactName { get; private set; } = null!;

    public string ContactPhone { get; private set; } = null!;

    public string? BuyerNote { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    /// <summary>
    /// When the stock reservation lapses while the order is still <see cref="OrderStatus.Pending"/>.
    /// Cleared once the merchant confirms — a confirmed order
    /// holds its stock until it is fulfilled or cancelled. The window itself is configuration,
    /// not a domain constant.
    /// </summary>
    public DateTime? ReservationExpiresAtUtc { get; private set; }

    public string? StatusReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Guards a buyer cancellation racing a merchant transition on the same order.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>True while the order still holds reserved stock that a later transition must release or consume.</summary>
    public bool HoldsReservation => Status is OrderStatus.Pending or OrderStatus.Confirmed
        or OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;

    public bool IsTerminal => Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.NoShow;

    /// <summary>A buyer may cancel only before the merchant has started fulfilling it.</summary>
    public bool BuyerCanCancel => Status is OrderStatus.Pending or OrderStatus.Confirmed;

    public bool MerchantCanCancel => Status is OrderStatus.Pending or OrderStatus.Confirmed
        or OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery;

    public int TotalUnits => _items.Sum(i => i.Quantity);

    // ---- Building --------------------------------------------------------------------

    /// <summary>Adds a variant line while the order is still being placed. Only valid before
    /// the order leaves <see cref="OrderStatus.Pending"/>.</summary>
    public OrderItem AddItem(
        Guid listingId,
        Guid listingVariantId,
        int quantity,
        decimal unitPriceSnapshot,
        string listingTitleSnapshot,
        string variantSnapshot,
        string conditionGradeSnapshot,
        string? discountReasonSnapshot)
    {
        if (Status != OrderStatus.Pending)
        {
            // Items are only ever added while the order is being placed.
            throw new DomainException("Items cannot be added to an order after it has been placed.");
        }

        if (_items.Any(i => i.ListingVariantId == listingVariantId))
        {
            throw new DomainException("That variant is already on this order.");
        }

        var item = new OrderItem(
            listingId, listingVariantId, quantity, unitPriceSnapshot,
            listingTitleSnapshot, variantSnapshot, conditionGradeSnapshot, discountReasonSnapshot);
        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    /// <summary>Recomputes the money totals from the line snapshots.</summary>
    public void RecalculateTotals()
    {
        Subtotal = _items.Sum(i => i.LineTotalSnapshot);
        Total = Subtotal;
    }

    // ---- Lifecycle ------------------------------------------------------------------

    public void Confirm(DateTime nowUtc)
    {
        RequireStatus(OrderStatus.Pending, "confirmed");

        if (ReservationExpiresAtUtc is { } expiresAt && expiresAt <= nowUtc)
        {
            // The stock reservation already lapsed. Confirming would keep it held on the
            // strength of a window that has passed; the expiry sweep must cancel it and
            // release the stock instead.
            throw new DomainException(
                "This order's stock reservation has expired. It will be released — ask the buyer to order again.");
        }

        Status = OrderStatus.Confirmed;
        ConfirmedAtUtc = nowUtc;
        // The reservation no longer lapses on its own; it is now held for the merchant to fulfil.
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    public void MarkReadyForPickup(DateTime nowUtc)
    {
        RequireStatus(OrderStatus.Confirmed, "marked ready for pickup");
        if (FulfillmentType != OrderFulfillmentType.Pickup)
        {
            throw new DomainException("Only a pickup order can be marked ready for pickup.");
        }

        Status = OrderStatus.ReadyForPickup;
        Touch(nowUtc);
    }

    public void MarkOutForDelivery(DateTime nowUtc)
    {
        RequireStatus(OrderStatus.Confirmed, "marked out for delivery");
        if (FulfillmentType != OrderFulfillmentType.MerchantDelivery)
        {
            throw new DomainException("Only a delivery order can be marked out for delivery.");
        }

        Status = OrderStatus.OutForDelivery;
        Touch(nowUtc);
    }

    public void Complete(DateTime nowUtc)
    {
        if (Status is not (OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery))
        {
            throw new DomainException($"An order in status {Status} cannot be completed.");
        }

        Status = OrderStatus.Completed;
        CompletedAtUtc = nowUtc;
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    public void Cancel(string reason, DateTime nowUtc)
    {
        if (IsTerminal)
        {
            throw new DomainException($"An order in status {Status} can no longer be cancelled.");
        }

        Status = OrderStatus.Cancelled;
        StatusReason = Require(reason, "cancellation reason", MaxStatusReasonLength);
        CancelledAtUtc = nowUtc;
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    public void MarkNoShow(string reason, DateTime nowUtc)
    {
        if (Status is not (OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery))
        {
            throw new DomainException($"An order in status {Status} cannot be marked as a no-show.");
        }

        Status = OrderStatus.NoShow;
        StatusReason = Require(reason, "no-show reason", MaxStatusReasonLength);
        ReservationExpiresAtUtc = null;
        Touch(nowUtc);
    }

    private void RequireStatus(OrderStatus expected, string verb)
    {
        if (Status != expected)
        {
            throw new DomainException($"An order in status {Status} cannot be {verb}.");
        }
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;

    private static string GenerateReference() =>
        new(RandomNumberGenerator.GetItems<char>(ReferenceAlphabet, ReferenceLength));

    private static string Require(string value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException($"The {field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    /// <summary>
    /// As <see cref="Require"/>, but a value over the limit is truncated instead of rejected.
    /// Used for server-composed descriptive snapshots whose length depends on how much
    /// (valid) free text the merchant entered — those must never block a checkout.
    /// </summary>
    private static string RequireThenTruncate(string value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException($"The {field} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
