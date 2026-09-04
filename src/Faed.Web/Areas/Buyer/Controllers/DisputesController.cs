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
/// A buyer's disputes over their own B2C orders. The dispute
/// service re-checks participation on every call, so a guessed id reveals nothing
/// </summary>
[Area("Buyer")]
[Authorize(Policy = FaedPolicies.CanPlaceB2COrder)]
public sealed class DisputesController(IDisputeService disputes, IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var mine = await disputes.GetMyDisputesAsync(User.RequireUserId(), page, cancellationToken);
        return View(new BuyerDisputeListPageModel { Disputes = mine });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetMyOrderAsync(User.RequireUserId(), orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return View(new FileDisputePageModel
        {
            OrderId = orderId,
            OrderReference = $"Order {orderId.ToString()[..8]} — {order.MerchantBusinessName}",
            Form = new FileDisputeFormModel { OrderId = orderId },
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(FileDisputeFormModel form, CancellationToken cancellationToken)
    {
        var order = await orders.GetMyOrderAsync(User.RequireUserId(), form.OrderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var page = new FileDisputePageModel
        {
            OrderId = form.OrderId,
            OrderReference = $"Order {form.OrderId.ToString()[..8]} — {order.MerchantBusinessName}",
            Form = form,
        };

        if (!ModelState.IsValid)
        {
            return View(page);
        }

        var result = await disputes.FileDisputeAsync(
            User.RequireUserId(),
            new FileDisputeInput(
                TrustTransactionType.B2COrder,
                form.OrderId,
                form.ReasonCode,
                form.Description,
                DisputeUploads.From(form.Evidence)),
            cancellationToken);

        if (result.Failed)
        {
            if (result.ErrorKind == ResultErrorKind.NotFound)
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, result.Error!);
            return View(page);
        }

        TempData["StatusMessage"] = "Your dispute has been filed. An administrator will review it.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await disputes.GetMyDisputeAsync(User.RequireUserId(), id, cancellationToken);
        return dispute is null ? NotFound() : View(new BuyerDisputeDetailPageModel { Dispute = dispute });
    }

    [HttpPost]
    public async Task<IActionResult> AddEvidence(Guid id, AddEvidenceFormModel form, CancellationToken cancellationToken)
    {
        var result = await disputes.AddEvidenceAsync(
            User.RequireUserId(), id, DisputeUploads.From(form.Files), cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? "Evidence added to the dispute." : result.Error;
        return result.ErrorKind == ResultErrorKind.NotFound
            ? NotFound()
            : RedirectToAction(nameof(Details), new { id });
    }
}
