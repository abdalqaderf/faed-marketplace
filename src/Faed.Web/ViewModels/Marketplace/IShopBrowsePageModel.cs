using Faed.Web.Services.Marketplace;

namespace Faed.Web.ViewModels.Marketplace;

/// <summary>
/// The shared shape of a filterable listing grid page (Shop and Merchant Store), so the
/// filter/sort/grid/pagination markup can live in one partial instead of being duplicated
/// (faed-marketplace-pages "Cross-page consistency").
/// </summary>
public interface IShopBrowsePageModel
{
    ShopResultView Result { get; }

    ShopFilterModel Filters { get; }
}
