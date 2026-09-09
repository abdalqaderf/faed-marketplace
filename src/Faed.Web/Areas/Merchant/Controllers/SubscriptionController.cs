using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// The merchant's subscription page: current plan and quota usage, how to pay, and the plan
/// chooser. Gated by <c>RegisteredMerchant</c> rather than <c>ApprovedMerchant</c> so an
/// unapproved merchant sees this page — with a message explaining verification comes first —
/// instead of an error page.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.RegisteredMerchant)]
public sealed class SubscriptionController(ISubscriptionService subscriptions) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = await subscriptions.GetMySubscriptionAsync(User.RequireUserId(), cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        return View(new SubscriptionPageModel { Page = page });
    }

    [HttpPost]
    public async Task<IActionResult> ChoosePlan(ChoosePlanFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync(cancellationToken, form.SubscriptionPlanId);
        }

        var result = await subscriptions.ChoosePlanAsync(User.RequireUserId(), form.SubscriptionPlanId, cancellationToken);
        if (result.Failed)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await RedisplayAsync(cancellationToken, form.SubscriptionPlanId);
        }

        TempData["StatusMessage"] = result.Value.ListingsPaused > 0
            ? $"Plan updated. {result.Value.ListingsPaused} listing(s) beyond your new quota were paused — you can choose which to restore."
            : "Plan updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RedisplayAsync(CancellationToken cancellationToken, Guid? selectedPlanId)
    {
        var page = await subscriptions.GetMySubscriptionAsync(User.RequireUserId(), cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        return View(nameof(Index), new SubscriptionPageModel { Page = page, SelectedPlanId = selectedPlanId });
    }
}
