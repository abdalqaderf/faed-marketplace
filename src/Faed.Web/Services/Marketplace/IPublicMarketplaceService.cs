namespace Faed.Web.Services.Marketplace;

/// <summary>
/// Anonymous-safe read use cases for the public marketplace (tasks/TASK-005-PUBLIC-MARKETPLACE.md).
/// Every method here only ever returns <c>Live</c> listings and <c>Approved</c> merchants —
/// non-public content has no code path through this service
/// (docs/03-BUSINESS-RULES.md §2, docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings").
/// </summary>
public interface IPublicMarketplaceService
{
    Task<HomePageView> GetHomePageAsync(CancellationToken cancellationToken = default);

    Task<ShopResultView> BrowseListingsAsync(ShopQuery query, CancellationToken cancellationToken = default);

    /// <summary>The public listing detail, or <c>null</c> when the slug does not resolve to a Live listing.</summary>
    Task<PublicListingDetailView?> GetListingBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>The storefront header, or <c>null</c> when the slug does not resolve to an Approved merchant.</summary>
    Task<PublicMerchantProfileView?> GetMerchantStoreHeaderBySlugAsync(
        string slug, CancellationToken cancellationToken = default);
}
