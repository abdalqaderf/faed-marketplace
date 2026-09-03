using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// A selling merchant's B2C order queue and per-order fulfilment actions
/// (tasks/TASK-006-B2C-ORDERS.md, docs/07-UI-UX-SPEC.md §6). Gated by the
/// <c>ApprovedMerchant</c> policy; the service re-checks that each order belongs to the
/// caller's own merchant (docs/03-BUSINESS-RULES.md §16).
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class OrdersController(IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        MerchantOrderFilter filter = MerchantOrderFilter.Open, CancellationToken cancellationToken = default)
    {
        var userId = User.RequireUserId();
        var items = await orders.GetMerchantOrdersAsync(userId, filter, cancellationToken);
        var needsConfirmation = filter == MerchantOrderFilter.NeedsConfirmation
            ? items.Count
            : await orders.GetMerchantOpenOrderCountAsync(userId, cancellationToken);

        return View(new MerchantOrderListPageModel
        {
            Filter = filter,
            Orders = items,
            NeedsConfirmationCount = needsConfirmation,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetMerchantOrderAsync(User.RequireUserId(), id, cancellationToken);
        return order is null ? NotFound() : View(new MerchantOrderDetailPageModel { Order = order });
    }

    [HttpPost]
    public Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.ConfirmAsync(User.RequireUserId(), id, cancellationToken), "Order confirmed.");

    [HttpPost]
    public Task<IActionResult> ReadyForPickup(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.MarkReadyForPickupAsync(User.RequireUserId(), id, cancellationToken),
            "Order marked ready for pickup.");

    [HttpPost]
    public Task<IActionResult> OutForDelivery(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.MarkOutForDeliveryAsync(User.RequireUserId(), id, cancellationToken),
            "Order marked out for delivery.");

    [HttpPost]
    public Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.CompleteAsync(User.RequireUserId(), id, cancellationToken),
            "Order completed. Reserved stock has been recorded as sold.");

    [HttpPost]
    public Task<IActionResult> NoShow(Guid id, string? reason, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.MarkNoShowAsync(
            User.RequireUserId(), id, reason ?? string.Empty, cancellationToken),
            "Order marked as a no-show. The reserved stock has been released.");

    [HttpPost]
    public Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken) =>
        ActAsync(id, () => orders.CancelAsMerchantAsync(
            User.RequireUserId(), id, reason ?? string.Empty, cancellationToken),
            "Order cancelled. The reserved stock has been released.");

    private async Task<IActionResult> ActAsync(Guid id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
        }
        else if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }
        else
        {
            TempData["ErrorMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
