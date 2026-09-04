using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One variant line of an accepted <see cref="B2BDeal"/>. Every
/// line corresponds to the accepted revision and stores immutable snapshots of the agreed unit
/// price and the variant combination — the deal never reads these back from the mutable
/// listing.
/// </summary>
public class B2BDealLine
{
    public const int MaxVariantSnapshotLength = 400;

    private B2BDealLine()
    {
    }

    internal B2BDealLine(Guid listingVariantId, int quantity, decimal unitPriceSnapshot, string variantSnapshot)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A deal line quantity must be greater than zero.");
        }

        if (unitPriceSnapshot < 0)
        {
            throw new DomainException("A deal line unit price cannot be negative.");
        }

        Id = Guid.CreateVersion7();
        ListingVariantId = listingVariantId;
        Quantity = quantity;
        UnitPriceSnapshot = unitPriceSnapshot;
        LineTotalSnapshot = unitPriceSnapshot * quantity;
        VariantSnapshot = Require(variantSnapshot);
    }

    public Guid Id { get; private set; }

    public Guid B2BDealId { get; private set; }

    public Guid ListingVariantId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPriceSnapshot { get; private set; }

    /// <summary>Server-calculated: <see cref="UnitPriceSnapshot"/> × <see cref="Quantity"/>.</summary>
    public decimal LineTotalSnapshot { get; private set; }

    public string VariantSnapshot { get; private set; } = null!;

    private static string Require(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "Variant";
        }

        return trimmed.Length > MaxVariantSnapshotLength ? trimmed[..MaxVariantSnapshotLength] : trimmed;
    }
}
