using Faed.Web.Services.Marketplace;

namespace Faed.Web.ViewModels.Marketplace;

public sealed class ShopPageModel : IShopBrowsePageModel
{
    public required ShopResultView Result { get; init; }

    public required ShopFilterModel Filters { get; init; }
}
