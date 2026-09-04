using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// The sellable SKU and the single authoritative stock record for a listing.
/// A listing never holds
/// an aggregate quantity: <c>Black / M = 4</c> and <c>Black / L = 2</c> are independent
/// records with independent concurrency protection.
/// <see cref="RowVersion"/> is a SQL Server <c>rowversion</c> present from the first
/// variant migration; every quantity movement must run inside a transaction
/// that fails rather than overwrite a competing one.
/// </summary>
public class ListingVariant
{
    public const int MaxSkuLength = 64;
    public const int MaxOptionCombinationKeyLength = 512;

    private readonly List<ListingVariantOptionValue> _optionValues = [];

    private ListingVariant()
    {
    }

    internal ListingVariant(
        string sku,
        IReadOnlyCollection<Guid> optionValueIds,
        int initialQuantity,
        DateTime nowUtc)
    {
        if (initialQuantity < 0)
        {
            throw new DomainException("Initial quantity cannot be negative.");
        }

        Id = Guid.CreateVersion7();
        Sku = sku;
        InitialQuantity = initialQuantity;
        AvailableQuantity = initialQuantity;
        ReservedQuantity = 0;
        SoldQuantity = 0;
        IsActive = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        foreach (var optionValueId in optionValueIds.Distinct())
        {
            _optionValues.Add(new ListingVariantOptionValue(optionValueId));
        }

        OptionCombinationKey = BuildCombinationKey(optionValueIds);
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    /// <summary>Merchant-facing stock keeping unit. Unique within its listing.</summary>
    public string Sku { get; private set; } = null!;

    /// <summary>
    /// Deterministic fingerprint of this variant's option-value combination. It exists so
    /// "one listing cannot have duplicate option-value combinations"
    /// is enforced by a unique database index, which a join
    /// table alone cannot express.
    /// </summary>
    public string OptionCombinationKey { get; private set; } = null!;

    public int InitialQuantity { get; private set; }

    public int AvailableQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public int SoldQuantity { get; private set; }

    /// <summary>An inactive variant is retained for history but cannot be newly purchased.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>SQL Server <c>rowversion</c> optimistic concurrency token.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ListingVariantOptionValue> OptionValues => _optionValues.AsReadOnly();

    /// <summary>True when this variant can currently satisfy a purchase of one unit.</summary>
    public bool IsSellable => IsActive && AvailableQuantity > 0;

    /// <summary>
    /// Applies a manual stock correction and returns the new available quantity. Callers
    /// record the movement as an <see cref="InventoryAdjustment"/> in the same transaction —
    /// stock is never silently overwritten.
    /// </summary>
    public int AdjustAvailable(int quantityDelta, DateTime nowUtc)
    {
        if (quantityDelta == 0)
        {
            throw new DomainException("A stock adjustment must change the quantity.");
        }

        var target = (long)AvailableQuantity + quantityDelta;
        if (target < 0)
        {
            throw new DomainException(
                $"The adjustment would leave {target} units. Available stock cannot go below zero.");
        }

        // InitialQuantity is deliberately not moved: it is the opening balance of the
        // stock-accounting invariant, and the adjustment totals live in InventoryAdjustment rows.
        AvailableQuantity = (int)target;
        UpdatedAtUtc = nowUtc;
        return AvailableQuantity;
    }

    /// <summary>
    /// Moves <paramref name="quantity"/> units from available to reserved for a B2C order.
    /// The caller runs this inside a transaction whose write
    /// is protected by <see cref="RowVersion"/>, so two orders racing for the last unit
    /// cannot both succeed.
    /// </summary>
    public void Reserve(int quantity, DateTime nowUtc)
    {
        RequirePositive(quantity);

        if (!IsActive)
        {
            throw new DomainException("This variant is not available for purchase.");
        }

        if (quantity > AvailableQuantity)
        {
            throw new DomainException(
                $"Only {AvailableQuantity} unit(s) of {Sku} are available.");
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Returns <paramref name="quantity"/> reserved units to available stock when an order is
    /// cancelled, expires, or is marked no-show. Each
    /// reservation is released exactly once over its lifecycle
    /// </summary>
    public void ReleaseReservation(int quantity, DateTime nowUtc)
    {
        RequirePositive(quantity);

        if (quantity > ReservedQuantity)
        {
            throw new DomainException(
                $"Cannot release {quantity} unit(s) of {Sku}; only {ReservedQuantity} are reserved.");
        }

        ReservedQuantity -= quantity;
        AvailableQuantity += quantity;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Converts <paramref name="quantity"/> reserved units into sold units when an order
    /// completes. Reserved stock never returns to available
    /// once it is sold.
    /// </summary>
    public void ConfirmSale(int quantity, DateTime nowUtc)
    {
        RequirePositive(quantity);

        if (quantity > ReservedQuantity)
        {
            throw new DomainException(
                $"Cannot complete {quantity} unit(s) of {Sku}; only {ReservedQuantity} are reserved.");
        }

        ReservedQuantity -= quantity;
        SoldQuantity += quantity;
        UpdatedAtUtc = nowUtc;
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A stock movement must be a positive number of units.");
        }
    }

    public void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        UpdatedAtUtc = nowUtc;
    }

    public void Reactivate(DateTime nowUtc)
    {
        IsActive = true;
        UpdatedAtUtc = nowUtc;
    }

    internal bool MatchesCombination(IReadOnlyCollection<Guid> optionValueIds) =>
        string.Equals(OptionCombinationKey, BuildCombinationKey(optionValueIds), StringComparison.Ordinal);

    /// <summary>
    /// Order-independent key for a set of option values, so <c>Black + M</c> and
    /// <c>M + Black</c> produce the same fingerprint.
    /// </summary>
    internal static string BuildCombinationKey(IReadOnlyCollection<Guid> optionValueIds) =>
        string.Join('|', optionValueIds.Distinct().Select(id => id.ToString("N")).Order(StringComparer.Ordinal));
}
