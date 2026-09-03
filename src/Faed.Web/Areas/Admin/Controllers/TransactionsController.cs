using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin monitoring of B2C orders and B2B deals (docs/07-UI-UX-SPEC.md §7,
/// docs/16-PERMISSIONS-MATRIX.md "Manage selling merchant's B2C order — Admin ✅
/// monitoring/support", "View unrelated B2B negotiation — Admin ✅ monitoring/support").
/// Read-only: an administrator sees the full transaction for support, but the B2C / B2B
/// state machines stay with their participants.
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class TransactionsController(IAdminOperationsService operations) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Orders(
        AdminOrderFilter filter = AdminOrderFilter.InProgress,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var rows = await operations.GetOrdersAsync(filter, page, cancellationToken);
        return View(new AdminOrderMonitorPageModel { Filter = filter, Orders = rows });
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetails(Guid id, CancellationToken cancellationToken)
    {
        var order = await operations.GetOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : View(new AdminOrderDetailPageModel { Order = order });
    }

    [HttpGet]
    public async Task<IActionResult> Deals(
        AdminDealFilter filter = AdminDealFilter.InProgress,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var rows = await operations.GetDealsAsync(filter, page, cancellationToken);
        return View(new AdminDealMonitorPageModel { Filter = filter, Deals = rows });
    }

    [HttpGet]
    public async Task<IActionResult> DealDetails(Guid id, CancellationToken cancellationToken)
    {
        var deal = await operations.GetDealAsync(id, cancellationToken);
        return deal is null ? NotFound() : View(new AdminDealDetailPageModel { Deal = deal });
    }
}
