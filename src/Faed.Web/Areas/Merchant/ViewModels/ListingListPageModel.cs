using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class ListingListPageModel
{
    public required MerchantListingFilter Filter { get; init; }

    public required PagedResult<MerchantListingListItem> Items { get; init; }
}
