using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;
using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin merchant-verification queue, review detail, decisions, and the authorized
/// private-document stream (tasks/TASK-002-MERCHANT-VERIFICATION.md).
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class MerchantVerificationController(IMerchantVerificationService verification) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(MerchantQueueFilter filter = MerchantQueueFilter.PendingReview, CancellationToken cancellationToken = default)
    {
        var items = await verification.GetQueueAsync(filter, cancellationToken);
        var pendingCount = filter == MerchantQueueFilter.PendingReview
            ? items.Count
            : (await verification.GetQueueAsync(MerchantQueueFilter.PendingReview, cancellationToken)).Count;

        return View(new MerchantQueuePageModel
        {
            Filter = filter,
            Items = items,
            PendingCount = pendingCount,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await verification.GetForReviewAsync(id, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await verification.ApproveAsync(User.RequireUserId(), id, cancellationToken);
        return AfterDecision(result, id, "Merchant approved.");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string reason, CancellationToken cancellationToken)
    {
        var result = await verification.RejectAsync(User.RequireUserId(), id, reason, cancellationToken);
        return AfterDecision(result, id, "Merchant application rejected.");
    }

    [HttpPost]
    public async Task<IActionResult> Suspend(Guid id, string reason, CancellationToken cancellationToken)
    {
        var result = await verification.SuspendAsync(User.RequireUserId(), id, reason, cancellationToken);
        return AfterDecision(result, id, "Merchant suspended.");
    }

    [HttpPost]
    public async Task<IActionResult> Reinstate(Guid id, CancellationToken cancellationToken)
    {
        var result = await verification.ReinstateAsync(User.RequireUserId(), id, cancellationToken);
        return AfterDecision(result, id, "Merchant reinstated.");
    }

    /// <summary>
    /// Streams a private verification document. There is no public URL to this content;
    /// access requires the admin policy and is written to the audit log by the service
    /// (docs/08-SECURITY-AND-PRIVACY.md §3).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
    {
        var result = await verification.OpenVerificationDocumentAsync(User.RequireUserId(), id, cancellationToken);
        if (result.Failed)
        {
            return result.ErrorKind == ResultErrorKind.NotFound ? NotFound() : Forbid();
        }

        // Serve as an attachment (never inline): the browser must not render merchant-
        // supplied bytes in the admin's session (docs/08-SECURITY-AND-PRIVACY.md §3-4).
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.OriginalFileName);
    }

    private IActionResult AfterDecision(Result result, Guid id, string successMessage)
    {
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
