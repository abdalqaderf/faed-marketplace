using Faed.Web.Services.Marketplace;
using Faed.Web.ViewModels.Marketplace;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Controllers;

/// <summary>
/// The public browse/filter experience. Anonymous by
/// design — individuals can buy but Faed has no seller-only content here
/// </summary>
[Route("shop")]
public sealed class ShopController(IPublicMarketplaceService marketplace) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(ShopFilterModel filters, CancellationToken cancellationToken)
    {
        var result = await marketplace.BrowseListingsAsync(filters.ToQuery(merchantSlug: null), cancellationToken);
        return View(new ShopPageModel { Result = result, Filters = filters });
    }
}
