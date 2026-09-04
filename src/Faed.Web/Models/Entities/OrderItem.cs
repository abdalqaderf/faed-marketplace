using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One variant line on a B2C order. Every price and descriptive field is a snapshot taken
/// when the order was placed and never changes afterwards
/// — the listing it came from can later be edited, repriced or
/// archived without rewriting history.
/// </summary>
public class OrderItem
{
    public const int MaxTitleSnapshotLength = 200;
    public const int MaxVariantSnapshotLength = 300;
    public const int MaxConditionSnapshotLength = 120;
    public const int MaxDiscountReasonSnapshotLength = 400;

    private OrderItem()
    {
    }

    internal OrderItem(
        Guid listingId,
        Guid listingVariantId,
        int quantity,
        decimal unitPriceSnapshot,
        string listingTitleSnapshot,
        string variantSnapshot,
        string conditionGradeSnapshot,
        string? discountReasonSnapshot)
    {
        if (quantity <= 0)
        {
            throw new DomainException("An order line must be for at least one unit.");
        }

        if (unitPriceSnapshot < 0)
        {
            throw new DomainException("A unit price cannot be negative.");
        }

        Id = Guid.CreateVersion7();
        ListingId = listingId;
        ListingVariantId = listingVariantId;
        Quantity = quantity;
        UnitPriceSnapshot = unitPriceSnapshot;
        LineTotalSnapshot = unitPriceSnapshot * quantity;
        ListingTitleSnapshot = Truncate(listingTitleSnapshot, MaxTitleSnapshotLength);
        VariantSnapshot = Truncate(variantSnapshot, MaxVariantSnapshotLength);
        ConditionGradeSnapshot = Truncate(conditionGradeSnapshot, MaxConditionSnapshotLength);
        DiscountReasonSnapshot = discountReasonSnapshot is null
            ? null
            : Truncate(discountReasonSnapshot, MaxDiscountReasonSnapshotLength);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ListingId { get; private set; }

    public Guid ListingVariantId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPriceSnapshot { get; private set; }

    public decimal LineTotalSnapshot { get; private set; }

    public string ListingTitleSnapshot { get; private set; } = null!;

    public string VariantSnapshot { get; private set; } = null!;

    public string ConditionGradeSnapshot { get; private set; } = null!;

    public string? DiscountReasonSnapshot { get; private set; }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
