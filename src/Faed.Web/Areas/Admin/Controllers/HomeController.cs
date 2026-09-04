using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin overview: one screen that says what is waiting for a decision across every MVP
/// queue. Read-only; the counts are live queries.
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class HomeController(IAdminOperationsService operations) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await operations.GetDashboardAsync(cancellationToken);
        return View(new AdminOverviewPageModel { Dashboard = dashboard });
    }
}
