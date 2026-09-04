using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// Append-only audit of a manual stock correction. Every adjustment records who changed what, the quantity
/// before and after, and a merchant-supplied reason, so a variant's available quantity can
/// always be explained rather than merely observed.
/// </summary>
public class InventoryAdjustment
{
    public const int MaxReasonLength = 500;

    private InventoryAdjustment()
    {
    }

    internal InventoryAdjustment(
        Guid listingVariantId,
        string changedByUserId,
        InventoryAdjustmentType adjustmentType,
        int quantityDelta,
        int quantityBefore,
        int quantityAfter,
        string reason,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        ListingVariantId = listingVariantId;
        ChangedByUserId = changedByUserId;
        AdjustmentType = adjustmentType;
        QuantityDelta = quantityDelta;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        Reason = reason;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid ListingVariantId { get; private set; }

    public string ChangedByUserId { get; private set; } = null!;

    public InventoryAdjustmentType AdjustmentType { get; private set; }

    public int QuantityDelta { get; private set; }

    public int QuantityBefore { get; private set; }

    public int QuantityAfter { get; private set; }

    public string Reason { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
}
