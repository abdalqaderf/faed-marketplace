namespace Faed.Web.Models.Entities;

/// <summary>
/// One selectable value of a <see cref="ListingOption"/> — <c>M</c>, <c>Black</c>,
/// <c>42</c> (docs/04-DOMAIN-MODEL.md §4). Values are merchant free text: the platform
/// deliberately does not ship a fixed size or colour vocabulary.
/// </summary>
public class ListingOptionValue
{
    public const int MaxValueLength = 64;

    private ListingOptionValue()
    {
    }

    internal ListingOptionValue(string value, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Value = value;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid ListingOptionId { get; private set; }

    public string Value { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public ListingOption Option { get; private set; } = null!;
}
