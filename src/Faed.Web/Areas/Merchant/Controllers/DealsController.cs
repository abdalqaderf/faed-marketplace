using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// A merchant's accepted wholesale deals: the fulfilment queue for deals it sells and the
/// deals it buys. Gated by the
/// <c>CanNegotiateB2B</c> policy; the service re-checks role eligibility and participation on
/// every call.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.CanNegotiateB2B)]
public sealed class DealsController(
    IB2BDealService deals, IReviewService reviews, IDisputeService disputes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        B2BDealFilter filter = B2BDealFilter.Active, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.RequireUserId();
        var items = await deals.GetMyDealsAsync(userId, filter, page, cancellationToken);
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
        var userId = User.RequireUserId();
        var deal = await deals.GetDealAsync(userId, id, cancellationToken);
        if (deal is null)
        {
            return NotFound();
        }

        var eligibility = await reviews.GetEligibilityAsync(
            userId, TrustTransactionType.B2BDeal, id, cancellationToken);

        var forThisDeal = await disputes.GetDisputesForTransactionAsync(
            userId, TrustTransactionType.B2BDeal, id, cancellationToken);
        // Only an Open/UnderReview dispute suppresses a new filing.
        var activeDispute = forThisDeal.FirstOrDefault(d => d.IsActive);

        return View(new B2BDealDetailPageModel
        {
            Deal = deal,
            ReviewEligibility = eligibility,
            ActiveDispute = activeDispute,
            PastDisputes = forThisDeal.Where(d => !d.IsActive).ToList(),
            CanRaiseDispute = activeDispute is null && deal.Status != B2BDealStatus.Cancelled,
        });
    }

    /// <summary>The buying merchant reviews the seller once the deal is completed
    ///.</summary>
    [HttpPost]
    public async Task<IActionResult> Review(Guid id, MerchantLeaveReviewFormModel form, CancellationToken cancellationToken)
    {
        var result = await reviews.SubmitReviewAsync(
            User.RequireUserId(),
            new SubmitReviewInput(TrustTransactionType.B2BDeal, id, form.Rating, form.Comment),
            cancellationToken);

        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "Thanks — your review of the seller has been posted.";
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
