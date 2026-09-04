using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin listing moderation queue, review detail and decisions
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class ListingModerationController(IListingModerationService moderation) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        ModerationQueueFilter filter = ModerationQueueFilter.PendingReview,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var items = await moderation.GetQueueAsync(filter, page, cancellationToken);
        var pendingCount = filter == ModerationQueueFilter.PendingReview
            ? items.TotalCount
            : await moderation.GetPendingCountAsync(cancellationToken);

        return View(new ListingModerationQueuePageModel
        {
            Filter = filter,
            Items = items,
            PendingCount = pendingCount,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var listing = await moderation.GetForModerationAsync(id, cancellationToken);
        return listing is null ? NotFound() : View(new ListingModerationDetailPageModel { Listing = listing });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, string? reviewNote, CancellationToken cancellationToken)
    {
        var result = await moderation.ApproveAsync(User.RequireUserId(), id, reviewNote, cancellationToken);
        return AfterDecision(result, id, "Listing approved and published.");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string reason, CancellationToken cancellationToken)
    {
        var result = await moderation.RejectAsync(User.RequireUserId(), id, reason, cancellationToken);
        return AfterDecision(result, id, "Listing rejected.");
    }

    [HttpPost]
    public async Task<IActionResult> Hide(Guid id, string reason, CancellationToken cancellationToken)
    {
        var result = await moderation.HideAsync(User.RequireUserId(), id, reason, cancellationToken);
        return AfterDecision(result, id, "Listing hidden from the public marketplace.");
    }

    [HttpPost]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await moderation.RestoreAsync(User.RequireUserId(), id, cancellationToken);
        return AfterDecision(result, id, "Listing republished.");
    }

    private IActionResult AfterDecision(Result result, Guid id, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = result.Error;
        return result.ErrorKind == ResultErrorKind.NotFound
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Details), new { id });
    }
}
