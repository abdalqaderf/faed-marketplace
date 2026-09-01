using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class ListingListPageModel
{
    public required MerchantListingFilter Filter { get; init; }

    public required IReadOnlyList<MerchantListingListItem> Items { get; init; }
}
