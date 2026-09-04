using Faed.Web.Services.Marketplace;
using Faed.Web.Services.Trust;
using Faed.Web.ViewModels.Marketplace;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Controllers;

/// <summary>
/// The public merchant storefront. Only an Approved
/// merchant has a reachable storefront — a Draft/Pending/Rejected/Suspended merchant's slug
/// 404s, the same way a non-Live listing does.
/// </summary>
[Route("store")]
public sealed class StoreController(
    IPublicMarketplaceService marketplace, IReviewService reviews) : Controller
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> Index(string slug, ShopFilterModel filters, CancellationToken cancellationToken)
    {
        var merchant = await marketplace.GetMerchantStoreHeaderBySlugAsync(slug, cancellationToken);
        if (merchant is null)
        {
            return NotFound();
        }

        var result = await marketplace.BrowseListingsAsync(filters.ToQuery(merchantSlug: slug), cancellationToken);
        var merchantReviews = await reviews.GetMerchantReviewsAsync(merchant.Id, 10, cancellationToken);
        return View(new StorePageModel
        {
            Merchant = merchant,
            Result = result,
            Filters = filters,
            Reviews = merchantReviews,
        });
    }
}
