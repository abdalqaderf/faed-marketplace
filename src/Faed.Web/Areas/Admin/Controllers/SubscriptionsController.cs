using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin subscription collection: activate a month, extend, cancel — each with a recorded
/// payment reference (<c>BUSINESS-MODEL.md</c> §6.4). Faed never handles the money itself.
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class SubscriptionsController(ISubscriptionService subscriptions) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        SubscriptionQueueFilter filter = SubscriptionQueueFilter.All, int page = 1, CancellationToken cancellationToken = default)
    {
        var items = await subscriptions.GetQueueAsync(filter, page, cancellationToken);
        return View(new SubscriptionQueuePageModel { Filter = filter, Items = items });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await subscriptions.GetForAdminAsync(id, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Activate(Guid id, SubscriptionPaymentFormModel form, CancellationToken cancellationToken)
    {
        var result = await subscriptions.ActivateAsync(User.RequireUserId(), id, form.PaymentReference, cancellationToken);
        return AfterDecision(result, id, "Subscription activated.");
    }

    [HttpPost]
    public async Task<IActionResult> Extend(Guid id, SubscriptionPaymentFormModel form, CancellationToken cancellationToken)
    {
        var result = await subscriptions.ExtendAsync(User.RequireUserId(), id, form.PaymentReference, cancellationToken);
        return AfterDecision(result, id, "Subscription extended by one month.");
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await subscriptions.CancelAsync(User.RequireUserId(), id, cancellationToken);
        return AfterDecision(result, id, "Subscription cancelled. The merchant's listings are hidden, not deleted.");
    }

    private IActionResult AfterDecision(Result result, Guid id, string successMessage)
    {
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
