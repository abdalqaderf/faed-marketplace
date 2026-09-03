using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin monitoring of the reviews left across the marketplace (docs/07-UI-UX-SPEC.md §7).
/// Read-only: a verified review is immutable once left (tasks/TASK-009-TRUST.md) — this
/// screen is oversight, so an administrator can spot a merchant with a run of low ratings or
/// an abusive comment and follow up through the dispute or account channels. No spec defines
/// a review takedown, so none is offered here (see PROJECT_STATUS.md "TASK-010").
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class ReviewsController(IAdminOperationsService operations) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var reviews = await operations.GetReviewsAsync(page, cancellationToken);
        return View(new AdminReviewMonitorPageModel { Reviews = reviews });
    }
}
