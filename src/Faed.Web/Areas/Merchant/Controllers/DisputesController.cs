using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// A merchant's disputes over the transactions it takes part in — the B2C orders it sells
/// and the wholesale deals it buys or sells. Every filing goes
/// through <see cref="IDisputeService.FileDisputeAsync"/>, which re-checks participation and
/// eligibility server-side regardless of which page started the flow.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class DisputesController(
    IDisputeService disputes, IB2BDealService deals, IOrderService orders) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var mine = await disputes.GetMyDisputesAsync(User.RequireUserId(), page, cancellationToken);
        return View(new MerchantDisputeListPageModel { Disputes = mine });
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        TrustTransactionType type, Guid id, CancellationToken cancellationToken)
    {
        var page = await BuildPageAsync(type, id, new MerchantFileDisputeFormModel
        {
            TransactionType = type,
            TransactionId = id,
        }, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost]
    public async Task<IActionResult> Create(MerchantFileDisputeFormModel form, CancellationToken cancellationToken)
    {
        var page = await BuildPageAsync(form.TransactionType, form.TransactionId, form, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(page);
        }

        var result = await disputes.FileDisputeAsync(
            User.RequireUserId(),
            new FileDisputeInput(
                form.TransactionType,
                form.TransactionId,
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
        return dispute is null ? NotFound() : View(new MerchantDisputeDetailPageModel { Dispute = dispute });
    }

    [HttpPost]
    public async Task<IActionResult> AddEvidence(Guid id, MerchantAddEvidenceFormModel form, CancellationToken cancellationToken)
    {
        var result = await disputes.AddEvidenceAsync(
            User.RequireUserId(), id, DisputeUploads.From(form.Files), cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? "Evidence added to the dispute." : result.Error;
        return result.ErrorKind == ResultErrorKind.NotFound
            ? NotFound()
            : RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Confirms the caller takes part in the referenced transaction (via the same read paths
    /// the deal / order pages use) and builds the "raise a dispute" page. Returns <c>null</c>
    /// when the transaction is not the caller's to dispute — the POST then 404s, exactly as a
    /// guessed id would.
    /// </summary>
    private async Task<MerchantFileDisputePageModel?> BuildPageAsync(
        TrustTransactionType type, Guid id, MerchantFileDisputeFormModel form, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();
        string reference;
        string backController;

        if (type == TrustTransactionType.B2BDeal)
        {
            var deal = await deals.GetDealAsync(userId, id, cancellationToken);
            if (deal is null)
            {
                return null;
            }

            reference = $"Deal {id.ToString()[..8]} — {deal.CounterpartyName}";
            backController = "Deals";
        }
        else
        {
            var order = await orders.GetMerchantOrderAsync(userId, id, cancellationToken);
            if (order is null)
            {
                return null;
            }

            reference = $"Order {id.ToString()[..8]} — {order.ContactName}";
            backController = "Orders";
        }

        return new MerchantFileDisputePageModel
        {
            TransactionType = type,
            TransactionId = id,
            TransactionReference = reference,
            BackController = backController,
            Form = form,
        };
    }
}
