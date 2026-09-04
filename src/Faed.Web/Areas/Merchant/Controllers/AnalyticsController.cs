using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// The merchant recovery dashboard. Every figure is recomputed server-side from the
/// merchant's own order / deal / listing data by <see cref="IMerchantAnalyticsService"/> —
/// the page shows no client-supplied or merchant-editable total
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class AnalyticsController(IMerchantAnalyticsService analytics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var view = await analytics.GetForOwnerAsync(User.RequireUserId(), cancellationToken);
        return View(new MerchantAnalyticsPageModel { Analytics = view });
    }
}
