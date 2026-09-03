using Faed.Web.Areas.Buyer.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Buyer.Controllers;

/// <summary>
/// A buyer's own B2C order history and detail (docs/07-UI-UX-SPEC.md §5). A buyer sees only
/// their own orders — the service filters by the signed-in user id, so guessing another
/// order id reveals nothing (docs/08-SECURITY-AND-PRIVACY.md §9,
/// docs/16-PERMISSIONS-MATRIX.md).
/// </summary>
[Area("Buyer")]
[Authorize(Policy = FaedPolicies.CanPlaceB2COrder)]
public sealed class OrdersController(IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mine = await orders.GetMyOrdersAsync(User.RequireUserId(), cancellationToken);
        return View(new BuyerOrderListPageModel { Orders = mine });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetMyOrderAsync(User.RequireUserId(), id, cancellationToken);
        return order is null ? NotFound() : View(new BuyerOrderDetailPageModel { Order = order });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var result = await orders.CancelMyOrderAsync(
            User.RequireUserId(), id, reason ?? "Cancelled by the buyer.", cancellationToken);
        return After(result, id, "Order cancelled. The reserved stock has been released.");
    }

    /// <summary>The buyer confirms they received the order, completing it
    /// (docs/01-PRD.md §4 "confirm receipt"; docs/03-BUSINESS-RULES.md §7).</summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmReceipt(Guid id, CancellationToken cancellationToken)
    {
        var result = await orders.ConfirmReceiptAsync(User.RequireUserId(), id, cancellationToken);
        return After(result, id, "Thanks — the order is marked complete. You can now review the merchant.");
    }

    private IActionResult After(Result result, Guid id, string successMessage)
    {
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
