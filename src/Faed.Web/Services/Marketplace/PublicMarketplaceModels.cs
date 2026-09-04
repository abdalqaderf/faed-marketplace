using Faed.Web.Models.Enums;
using Faed.Web.Services.Listings;

namespace Faed.Web.Services.Marketplace;

/// <summary>Which sales channel a browse query should restrict results to.</summary>
public enum MarketplaceChannel
{
    All = 0,
    RetailOnly = 1,
    WholesaleOnly = 2,
}

public enum ShopSort
{
    Newest = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
}

/// <summary>
/// Everything a browse request can filter, sort and page by. Every catalog reference is a
/// public slug/code, never a database id — an unresolvable
/// value must yield zero results rather than being silently ignored.
/// </summary>
public sealed record ShopQuery(
    string? CategorySlug,
    string? ConditionCode,
    string? DiscountReasonCode,
    string? BrandSlug,
    string? SizeValue,
    string? ColorValue,
    decimal? MinPrice,
    decimal? MaxPrice,
    MarketplaceChannel Channel,
    ShopSort Sort,
    string? SearchText,
    string? MerchantSlug,
    int Page,
    int PageSize)
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;
}

/// <summary>A listing summary for a product grid (Home, Shop, Merchant Store).</summary>
public sealed record ListingCardView(
    Guid Id,
    string Title,
    string Slug,
    string MerchantBusinessName,
    string MerchantSlug,
    bool MerchantIsVerified,
    string CategoryName,
    string ConditionCode,
    string ConditionName,
    decimal? RetailPrice,
    decimal? ReferencePrice,
    bool AllowB2C,
    bool AllowB2B,
    decimal? WholesaleIndicativeUnitPrice,
    int? WholesaleMinQuantity,
    int AvailableUnits,
    Guid? PrimaryImageId,
    string? PrimaryImageAlt,
    string? PrimaryDiscountReasonName,
    int DiscountReasonCount)
{
    /// <summary>
    /// A reference price is only shown as a discount claim once the listing is Live — that is
    /// the point admin moderation vouched for it. <see cref="Listing.DescribeSubmissionBlockers"/> already
    /// refuses to publish a reference price that is not higher than the retail price.
    /// </summary>
    public bool HasValidReferencePrice => ReferencePrice is { } reference && RetailPrice is { } retail && reference > retail;

    public int? DiscountPercent => HasValidReferencePrice
        ? (int)Math.Round((1 - (RetailPrice!.Value / ReferencePrice!.Value)) * 100m)
        : null;

    /// <summary>
    /// The price to display/sort/filter on when the listing itself has no retail price — a
    /// B2B-only listing still has an honest number to show instead of "Price on request"
    /// </summary>
    public decimal? EffectivePrice => RetailPrice ?? WholesaleIndicativeUnitPrice;

    /// <summary>True when <see cref="EffectivePrice"/> comes from the wholesale price, not retail.</summary>
    public bool EffectivePriceIsWholesale => RetailPrice is null && WholesaleIndicativeUnitPrice is not null;

    public bool IsSoldOut => AvailableUnits <= 0;

    public bool IsLowStock => !IsSoldOut && AvailableUnits <= 3;

    public bool HasMoreReasons => DiscountReasonCount > 1;
}

public sealed record FacetOption(string Value, string Label);

/// <summary>The DB-driven filter choices for a browse page. Always the full reference list
/// (categories/conditions/reasons are small and admin-managed), except brands — an optional,
/// uncontrolled dimension shown only when at least one live listing actually uses one.</summary>
public sealed record ShopFacets(
    IReadOnlyList<FacetOption> Categories,
    IReadOnlyList<FacetOption> Conditions,
    IReadOnlyList<FacetOption> DiscountReasons,
    IReadOnlyList<FacetOption> Brands,
    IReadOnlyList<FacetOption> Sizes,
    IReadOnlyList<FacetOption> Colors);

public sealed record ShopResultView(
    IReadOnlyList<ListingCardView> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ShopFacets Facets,
    ShopQuery Query)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasResults => Items.Count > 0;
}

/// <summary>
/// A variant as shown to a buyer: no reserved/sold counters.
/// </summary>
public sealed record PublicListingVariantView(
    Guid Id,
    IReadOnlyList<VariantOptionView> Options,
    int AvailableQuantity,
    bool IsActive)
{
    public bool IsSellable => IsActive && AvailableQuantity > 0;

    public string Combination => Options.Count == 0
        ? "One size"
        : string.Join(" · ", Options.Select(o => $"{o.Option}: {o.Value}"));
}

/// <summary>
/// Everything an anonymous or signed-in buyer may see about a Live listing. Deliberately a
/// separate shape from the merchant/admin <see cref="ListingDetailView"/>: moderation history,
/// blockers and hidden-by-admin flags are internal review state, not public content
/// </summary>
public sealed record PublicListingDetailView(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string CategoryName,
    string CategorySlug,
    string? BrandName,
    string ConditionCode,
    string ConditionName,
    string ConditionDescription,
    decimal? ReferencePrice,
    decimal? RetailPrice,
    decimal? WholesaleIndicativeUnitPrice,
    int? WholesaleMinQuantity,
    bool AllowB2C,
    bool AllowB2B,
    bool AllowMixedVariantB2B,
    string? ReturnPolicyText,
    string? WarrantyText,
    string? IncludedItemsText,
    string? MissingItemsText,
    IReadOnlyList<string> DiscountReasonNames,
    IReadOnlyList<ListingOptionView> Options,
    IReadOnlyList<PublicListingVariantView> Variants,
    IReadOnlyList<ListingImageView> Media,
    Guid MerchantProfileId,
    string MerchantBusinessName,
    string MerchantSlug,
    bool MerchantIsVerified,
    DateTime PublishedAtUtc)
{
    public bool HasValidReferencePrice => ReferencePrice is { } reference && RetailPrice is { } retail && reference > retail;

    public int? DiscountPercent => HasValidReferencePrice
        ? (int)Math.Round((1 - (RetailPrice!.Value / ReferencePrice!.Value)) * 100m)
        : null;

    public IReadOnlyList<ListingImageView> ProductPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Product).OrderBy(m => m.SortOrder)];

    public IReadOnlyList<ListingImageView> DefectPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Defect).OrderBy(m => m.SortOrder)];

    public IReadOnlyList<ListingImageView> PackagingPhotos =>
        [.. Media.Where(m => m.MediaType == ListingMediaType.Packaging).OrderBy(m => m.SortOrder)];

    public bool HasDisclosedIssues => DefectPhotos.Count > 0 || PackagingPhotos.Count > 0;

    public int AvailableUnits => Variants.Where(v => v.IsActive).Sum(v => v.AvailableQuantity);

    /// <summary>
    /// The <see cref="ListingOptionValueView.Id"/>s carried by at least one sellable variant.
    /// The variant picker greys out — and refuses selection of — any value not in this set,
    /// both server-side in the view and client-side in <c>listing-detail.js</c>.
    /// <para>
    /// Deliberately a per-value test, never a per-combination one. Disabling a value because
    /// it clashes with the currently selected value in another option group traps the buyer:
    /// from a valid <c>Black/M</c> they could never reach a valid <c>White/L</c>, because
    /// every <c>White</c> chip would disable itself against the selected size <c>M</c>
    /// (faed-commerce-ux "let the buyer move between valid combinations"). Impossible partial
    /// combinations are surfaced by the availability line instead.
    /// </para>
    /// </summary>
    public IReadOnlySet<Guid> SellableOptionValueIds =>
        Options
            .SelectMany(o => o.Values, (o, v) => (Option: o.Name, v.Id, v.Value))
            .Where(x => Variants.Any(variant => variant.IsSellable
                && variant.Options.Any(vo => vo.Option == x.Option && vo.Value == x.Value)))
            .Select(x => x.Id)
            .ToHashSet();

    public bool IsSoldOut => AvailableUnits <= 0;

    public bool IsLowStock => !IsSoldOut && AvailableUnits <= 3;
}

/// <summary>The public header of a merchant storefront.</summary>
public sealed record PublicMerchantProfileView(
    Guid Id,
    string BusinessName,
    string PublicSlug,
    bool IsVerified,
    DateTime MemberSinceUtc,
    int LiveListingCount);

public sealed record CategoryNavItem(string Slug, string Name, int LiveListingCount);

public sealed record HomePageView(
    IReadOnlyList<ListingCardView> FeaturedListings,
    IReadOnlyList<CategoryNavItem> Categories,
    int LiveListingCount,
    int VerifiedMerchantCount);
