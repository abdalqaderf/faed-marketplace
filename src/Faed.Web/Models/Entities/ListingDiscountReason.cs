namespace Faed.Web.Models.Entities;

/// <summary>
/// Join row linking a listing to one admin-managed <see cref="Entities.DiscountReason"/>.
/// A listing may carry several reasons — "past season" and "packaging damage" together
/// Reasons are kept structurally independent of the listing's
/// <see cref="Entities.ConditionGrade"/>: why an item is discounted is not what physical
/// state it is in.
/// </summary>
public class ListingDiscountReason
{
    private ListingDiscountReason()
    {
    }

    internal ListingDiscountReason(Guid discountReasonId)
    {
        DiscountReasonId = discountReasonId;
    }

    public Guid ListingId { get; private set; }

    public Guid DiscountReasonId { get; private set; }

    public DiscountReason DiscountReason { get; private set; } = null!;
}
