namespace Faed.Web.Models.Entities;

/// <summary>
/// Join row binding a sellable <see cref="ListingVariant"/> to one
/// <see cref="ListingOptionValue"/> (docs/04-DOMAIN-MODEL.md §4). A variant carries exactly
/// one row per listing option, so <c>Black / M</c> is three rows away from <c>White / M</c>
/// without either being a distinct entity type.
///
/// The combination is additionally denormalised onto
/// <see cref="ListingVariant.OptionCombinationKey"/> so uniqueness is enforced by a database
/// index rather than only in application code.
/// </summary>
public class ListingVariantOptionValue
{
    private ListingVariantOptionValue()
    {
    }

    internal ListingVariantOptionValue(Guid listingOptionValueId)
    {
        ListingOptionValueId = listingOptionValueId;
    }

    public Guid ListingVariantId { get; private set; }

    public Guid ListingOptionValueId { get; private set; }

    public ListingOptionValue OptionValue { get; private set; } = null!;
}
