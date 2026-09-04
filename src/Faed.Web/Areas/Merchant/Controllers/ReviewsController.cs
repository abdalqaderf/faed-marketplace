using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// The reviews a merchant has received from buyers and wholesale buying merchants
/// (docs/07-UI-UX-SPEC.md §4 "aggregate trust signals", docs/03-BUSINESS-RULES.md §13).
/// Read-only — a merchant never edits or removes a review.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class ReviewsController(IReviewService reviews) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var received = await reviews.GetReviewsForOwnerAsync(User.RequireUserId(), page, cancellationToken);
        return View(new MerchantReviewsPageModel { Reviews = received });
    }
}
