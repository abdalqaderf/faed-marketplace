namespace Faed.Web.Models.Entities;

/// <summary>
/// One dimension a listing's stock varies along — <c>Size</c>, <c>Colour</c> — held
/// generically rather than as hard-coded columns, so a T-shirt (Colour × Size) and a shoe
/// (Size only) use the same schema (docs/04-DOMAIN-MODEL.md §4, AGENTS.md Rule A).
/// </summary>
public class ListingOption
{
    public const int MaxNameLength = 64;

    private readonly List<ListingOptionValue> _values = [];

    private ListingOption()
    {
    }

    internal ListingOption(string name, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    public string Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public IReadOnlyCollection<ListingOptionValue> Values => _values.AsReadOnly();

    internal ListingOptionValue AddValue(string value, int sortOrder)
    {
        var optionValue = new ListingOptionValue(value, sortOrder);
        _values.Add(optionValue);
        return optionValue;
    }

    internal bool RemoveValue(Guid valueId)
    {
        var value = _values.SingleOrDefault(v => v.Id == valueId);
        return value is not null && _values.Remove(value);
    }

    internal bool HasValue(string value) =>
        _values.Any(v => string.Equals(v.Value, value, StringComparison.OrdinalIgnoreCase));
}
