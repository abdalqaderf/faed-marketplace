using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Faed.Web.Services.Marketplace;

namespace Faed.Web.ViewModels.Marketplace;

/// <summary>
/// Query-string bound filter/sort/paging state for the Shop and Merchant Store browse pages.
/// A mutable class (not the <see cref="ShopQuery"/> record) because GET model binding needs
/// public settable properties, and because the same instance is round-tripped straight back
/// into filter form fields.
///
/// Every value is attacker-supplied query string. <see cref="ToQuery"/> is the single point
/// that hands a sanitized shape to the service: prices are clamped non-negative and a reversed
/// range is corrected, search text is trimmed and length-capped, and an out-of-range enum
/// falls back to its default. The bound properties also carry <see cref="ValidationAttribute"/>s
/// so a caller that wants to surface "that price is invalid" can inspect <c>ModelState</c>.
/// </summary>
public sealed class ShopFilterModel
{
    public const int MaxSearchTextLength = 100;

    /// <summary>Upper bound accepted from a price filter — well past any realistic listing
    /// price, but finite so a crafted enormous value cannot reach the query.</summary>
    public const double MaxPriceFilter = 9_999_999d;

    public string? Category { get; set; }

    public string? Condition { get; set; }

    public string? Reason { get; set; }

    public string? Brand { get; set; }

    public string? Size { get; set; }

    public string? Color { get; set; }

    [Range(0, MaxPriceFilter, ErrorMessage = "Enter a price of zero or more.")]
    public decimal? MinPrice { get; set; }

    [Range(0, MaxPriceFilter, ErrorMessage = "Enter a price of zero or more.")]
    public decimal? MaxPrice { get; set; }

    [EnumDataType(typeof(MarketplaceChannel))]
    public MarketplaceChannel Channel { get; set; } = MarketplaceChannel.All;

    [EnumDataType(typeof(ShopSort))]
    public ShopSort Sort { get; set; } = ShopSort.Newest;

    [StringLength(MaxSearchTextLength, ErrorMessage = "Keep the search text under {1} characters.")]
    public string? Q { get; set; }

    public int Page { get; set; } = 1;

    public ShopQuery ToQuery(string? merchantSlug)
    {
        var priceCeiling = (decimal)MaxPriceFilter;
        var minPrice = MinPrice is { } min && min >= 0 ? Math.Min(min, priceCeiling) : (decimal?)null;
        var maxPrice = MaxPrice is { } max && max >= 0 ? Math.Min(max, priceCeiling) : (decimal?)null;
        if (minPrice is { } lo && maxPrice is { } hi && lo > hi)
        {
            // A reversed range is a typo, not an empty result: swap it rather than silently
            // returning nothing for every listing.
            (minPrice, maxPrice) = (hi, lo);
        }

        var channel = Enum.IsDefined(Channel) ? Channel : MarketplaceChannel.All;
        var sort = Enum.IsDefined(Sort) ? Sort : ShopSort.Newest;

        var searchText = Q?.Trim();
        if (!string.IsNullOrEmpty(searchText) && searchText.Length > MaxSearchTextLength)
        {
            searchText = searchText[..MaxSearchTextLength];
        }

        var page = Page < 1 ? 1 : Page;

        return new(
            Category, Condition, Reason, Brand, Size, Color, minPrice, maxPrice, channel, sort,
            string.IsNullOrEmpty(searchText) ? null : searchText, merchantSlug,
            page, ShopQuery.DefaultPageSize);
    }

    /// <summary>
    /// How many distinct filters the reader currently has applied — the count the mobile
    /// filter control shows so "one filter" and "several filters" are visibly different
    /// (faed-marketplace-pages "clear filter count"). A min/max price pair counts once.
    /// </summary>
    public int ActiveFilterCount =>
        (!string.IsNullOrWhiteSpace(Category) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Condition) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Reason) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Brand) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Size) ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Color) ? 1 : 0) +
        (MinPrice is not null || MaxPrice is not null ? 1 : 0) +
        (Channel != MarketplaceChannel.All ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(Q) ? 1 : 0);

    public bool HasActiveFilters => ActiveFilterCount > 0;

    /// <summary>
    /// Route values for a pagination/sort link that must carry the current filters forward.
    /// <c>Page</c> is deliberately excluded — callers combine this with an explicit
    /// <c>asp-route-Page</c>, since the tag helper throws if a key appears in both places.
    /// </summary>
    public IDictionary<string, string?> ToFilterRouteValues() => new Dictionary<string, string?>
    {
        ["Category"] = Category,
        ["Condition"] = Condition,
        ["Reason"] = Reason,
        ["Brand"] = Brand,
        ["Size"] = Size,
        ["Color"] = Color,
        ["MinPrice"] = MinPrice?.ToString(CultureInfo.InvariantCulture),
        ["MaxPrice"] = MaxPrice?.ToString(CultureInfo.InvariantCulture),
        ["Channel"] = Channel == MarketplaceChannel.All ? null : Channel.ToString(),
        ["Sort"] = Sort == ShopSort.Newest ? null : Sort.ToString(),
        ["Q"] = Q,
    };
}
