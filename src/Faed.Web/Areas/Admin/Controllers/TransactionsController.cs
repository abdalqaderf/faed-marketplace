using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin monitoring of B2C orders.
/// Read-only: an administrator sees the full order for support, but the order state machine
/// stays with its participants.
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
}
