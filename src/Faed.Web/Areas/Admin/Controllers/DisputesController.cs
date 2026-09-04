using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin dispute queue and resolution workflow. Every decision is recorded to the admin audit log by the
/// service; the service also
/// re-checks the admin role, so the route policy is not the only guard
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class DisputesController(IDisputeService disputes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        DisputeQueueFilter filter = DisputeQueueFilter.Active, int page = 1, CancellationToken cancellationToken = default)
    {
        var items = await disputes.GetQueueAsync(filter, page, cancellationToken);
        var openCount = await disputes.GetOpenDisputeCountAsync(cancellationToken);

        return View(new DisputeQueuePageModel
        {
            Filter = filter,
            Items = items,
            OpenCount = openCount,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await disputes.GetForReviewAsync(id, cancellationToken);
        return dispute is null ? NotFound() : View(new DisputeReviewPageModel { Dispute = dispute });
    }

    [HttpPost]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await disputes.StartReviewAsync(User.RequireUserId(), id, cancellationToken);
        return After(result, id, "Dispute moved to review.");
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(Guid id, string resolution, CancellationToken cancellationToken)
    {
        var result = await disputes.ResolveAsync(User.RequireUserId(), id, resolution ?? string.Empty, cancellationToken);
        return After(result, id, "Dispute resolved. The outcome has been recorded.");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string resolution, CancellationToken cancellationToken)
    {
        var result = await disputes.RejectAsync(User.RequireUserId(), id, resolution ?? string.Empty, cancellationToken);
        return After(result, id, "Dispute dismissed. The outcome has been recorded.");
    }

    private IActionResult After(Result result, Guid id, string successMessage)
    {
        if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
