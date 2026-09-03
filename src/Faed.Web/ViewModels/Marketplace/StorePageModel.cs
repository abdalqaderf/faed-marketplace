using Faed.Web.Services.Marketplace;
using Faed.Web.Services.Trust;

namespace Faed.Web.ViewModels.Marketplace;

public sealed class StorePageModel : IShopBrowsePageModel
{
    public required PublicMerchantProfileView Merchant { get; init; }

    public required ShopResultView Result { get; init; }

    public required ShopFilterModel Filters { get; init; }

    /// <summary>Aggregate rating and recent reviews for the storefront (docs/07-UI-UX-SPEC.md §4).</summary>
    public MerchantReviewsView? Reviews { get; init; }
}
