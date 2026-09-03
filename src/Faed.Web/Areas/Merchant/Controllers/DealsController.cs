using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// A merchant's accepted wholesale deals: the fulfilment queue for deals it sells and the
/// deals it buys (tasks/TASK-008-B2B-DEALS.md, docs/07-UI-UX-SPEC.md §6). Gated by the
/// <c>CanNegotiateB2B</c> policy; the service re-checks role eligibility and participation on
/// every call (docs/16-PERMISSIONS-MATRIX.md).
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.CanNegotiateB2B)]
public sealed class DealsController(IB2BDealService deals) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        B2BDealFilter filter = B2BDealFilter.Active, CancellationToken cancellationToken = default)
    {
        var userId = User.RequireUserId();
        var items = await deals.GetMyDealsAsync(userId, filter, cancellationToken);
        var actionable = await deals.GetActionableDealCountAsync(userId, cancellationToken);

        return View(new B2BDealListPageModel
        {
            Filter = filter,
            Deals = items,
            ActionableCount = actionable,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var deal = await deals.GetDealAsync(User.RequireUserId(), id, cancellationToken);
        return deal is null ? NotFound() : View(new B2BDealDetailPageModel { Deal = deal });
    }

    [HttpPost]
    public Task<IActionResult> ReadyForPickup(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.MarkReadyForPickupAsync(User.RequireUserId(), id, cancellationToken),
            "Deal marked ready for pickup.");

    [HttpPost]
    public Task<IActionResult> Shipped(Guid id, string? shipmentReference, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.MarkShippedAsync(User.RequireUserId(), id, shipmentReference, cancellationToken),
            "Deal marked shipped.");

    [HttpPost]
    public Task<IActionResult> ShipmentReference(Guid id, string? shipmentReference, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.SetShipmentReferenceAsync(
            User.RequireUserId(), id, shipmentReference ?? string.Empty, cancellationToken),
            "Shipment reference saved.");

    [HttpPost]
    public Task<IActionResult> Delivered(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.MarkDeliveredAsync(User.RequireUserId(), id, cancellationToken),
            "Deal marked delivered.");

    [HttpPost]
    public Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.CompleteAsync(User.RequireUserId(), id, cancellationToken),
            "Deal completed. Reserved stock has been recorded as sold.");

    [HttpPost]
    public Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken) =>
        ActAsync(id, () => deals.CancelAsync(User.RequireUserId(), id, reason ?? string.Empty, cancellationToken),
            "Deal cancelled. The reserved stock has been released.");

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
