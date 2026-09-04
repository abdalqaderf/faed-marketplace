using Faed.Web.Areas.Buyer.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;
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
public sealed class OrdersController(
    IOrderService orders, IReviewService reviews, IDisputeService disputes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var mine = await orders.GetMyOrdersAsync(User.RequireUserId(), page, cancellationToken);
        return View(new BuyerOrderListPageModel { Orders = mine });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        var order = await orders.GetMyOrderAsync(userId, id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var eligibility = await reviews.GetEligibilityAsync(
            userId, TrustTransactionType.B2COrder, id, cancellationToken);

        var forThisOrder = await disputes.GetDisputesForTransactionAsync(
            userId, TrustTransactionType.B2COrder, id, cancellationToken);
        // Only an Open/UnderReview dispute suppresses a new filing; a closed one is history
        // and the authoritative rules may allow another dispute (docs/03-BUSINESS-RULES.md §14).
        var activeDispute = forThisOrder.FirstOrDefault(d => d.IsActive);

        return View(new BuyerOrderDetailPageModel
        {
            Order = order,
            ReviewEligibility = eligibility,
            ActiveDispute = activeDispute,
            PastDisputes = forThisOrder.Where(d => !d.IsActive).ToList(),
            CanRaiseDispute = activeDispute is null && order.Status is OrderStatus.Confirmed
                or OrderStatus.ReadyForPickup or OrderStatus.OutForDelivery or OrderStatus.Completed,
        });
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

    /// <summary>The buyer leaves a review for the selling merchant once the order is completed
    /// (docs/03-BUSINESS-RULES.md §13).</summary>
    [HttpPost]
    public async Task<IActionResult> Review(Guid id, LeaveReviewFormModel form, CancellationToken cancellationToken)
    {
        var result = await reviews.SubmitReviewAsync(
            User.RequireUserId(),
            new SubmitReviewInput(TrustTransactionType.B2COrder, id, form.Rating, form.Comment),
            cancellationToken);

        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "Thanks — your review has been posted.";
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
