using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// A merchant's B2B negotiation queue: making wholesale offers on other merchants' listings
/// and responding to offers on its own (tasks/TASK-007-B2B-NEGOTIATION.md,
/// docs/07-UI-UX-SPEC.md §6). Gated by the <c>CanNegotiateB2B</c> policy; the service
/// re-checks role eligibility and participation on every call (docs/16-PERMISSIONS-MATRIX.md).
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.CanNegotiateB2B)]
public sealed class OffersController(IB2BNegotiationService negotiations, IB2BDealService deals) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        B2BNegotiationFilter filter = B2BNegotiationFilter.AwaitingMe,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var userId = User.RequireUserId();
        var items = await negotiations.GetMyNegotiationsAsync(userId, filter, page, cancellationToken);
        var awaitingMe = await negotiations.GetAwaitingResponseCountAsync(userId, cancellationToken);

        return View(new B2BNegotiationListPageModel
        {
            Filter = filter,
            Negotiations = items,
            AwaitingMeCount = awaitingMe,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var negotiation = await negotiations.GetNegotiationAsync(User.RequireUserId(), id, cancellationToken);
        if (negotiation is null)
        {
            return NotFound();
        }

        return View(new B2BNegotiationDetailPageModel
        {
            Negotiation = negotiation,
            CounterForm = BuildCounterForm(negotiation),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(string listingSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(listingSlug))
        {
            return NotFound();
        }

        var result = await negotiations.GetListingForOfferAsync(User.RequireUserId(), listingSlug, cancellationToken);
        return RenderCreate(result, listingSlug);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Form")] B2BOfferFormModel form, CancellationToken cancellationToken)
    {
        var listing = await negotiations.GetListingForOfferAsync(User.RequireUserId(), form.ListingSlug, cancellationToken);
        if (listing.Failed)
        {
            return RenderCreate(listing, form.ListingSlug);
        }

        if (!ModelState.IsValid)
        {
            return View(new B2BOfferCreatePageModel { Listing = listing.Value, Form = Rehydrate(form, listing.Value) });
        }

        var placed = await negotiations.StartNegotiationAsync(User.RequireUserId(), new StartNegotiationInput(
            form.ListingSlug, form.ToLineInputs(), form.ProposedUnitPrice, form.Message, form.ValidityDays), cancellationToken);

        if (placed.Failed)
        {
            ModelState.AddModelError(string.Empty, placed.Error!);
            return View(new B2BOfferCreatePageModel { Listing = listing.Value, Form = Rehydrate(form, listing.Value) });
        }

        TempData["StatusMessage"] = "Offer sent. The seller can accept, reject or counter it.";
        return RedirectToAction(nameof(Details), new { id = placed.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Counter(Guid id, B2BOfferFormModel form, CancellationToken cancellationToken)
    {
        var result = await negotiations.CounterOfferAsync(User.RequireUserId(), id, new CounterOfferInput(
            form.ToLineInputs(), form.ProposedUnitPrice, form.Message, form.ValidityDays), cancellationToken);
        return await AfterActionAsync(id, result, "Counter-offer sent.");
    }

    [HttpPost]
    public async Task<IActionResult> Accept(Guid id, B2BFulfillmentType fulfillmentType, CancellationToken cancellationToken)
    {
        // Accepting the offer atomically reserves every requested variant and creates the
        // wholesale deal on the agreed terms alone (TASK-008, docs/03-BUSINESS-RULES.md §10).
        // Shipping information is recorded later by the selling merchant.
        var result = await deals.AcceptOfferAsync(
            User.RequireUserId(), id, new AcceptOfferInput(fulfillmentType), cancellationToken);

        if (result.Succeeded)
        {
            TempData["StatusMessage"] =
                "Offer accepted. The stock is reserved for the wholesale deal — arrange fulfilment from B2B Deals.";
            return RedirectToAction("Details", "Deals", new { id = result.Value });
        }

        if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }

        TempData["ErrorMessage"] = result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => negotiations.RejectAsync(User.RequireUserId(), id, cancellationToken), "Offer rejected.");

    [HttpPost]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => negotiations.CancelAsync(User.RequireUserId(), id, cancellationToken),
            "Negotiation cancelled.");

    private Task<IActionResult> ActAsync(Guid id, Func<Task<Result>> action, string successMessage) =>
        RunAsync(id, action, successMessage);

    private Task<IActionResult> AfterActionAsync(Guid id, Result result, string successMessage) =>
        RunAsync(id, () => Task.FromResult(result), successMessage);

    private async Task<IActionResult> RunAsync(Guid id, Func<Task<Result>> action, string successMessage)
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

    private IActionResult RenderCreate(Result<OfferListingView> result, string listingSlug)
    {
        if (result.Succeeded)
        {
            return View(new B2BOfferCreatePageModel
            {
                Listing = result.Value,
                Form = NewForm(result.Value),
            });
        }

        if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }

        TempData["ErrorMessage"] = result.Error;
        return RedirectToAction("Details", "Listing", new { area = "", slug = listingSlug });
    }

    private static B2BOfferFormModel NewForm(OfferListingView listing) => new()
    {
        ListingSlug = listing.Slug,
        ProposedUnitPrice = listing.IndicativeUnitPrice ?? 0m,
        ValidityDays = 3,
        Lines = listing.Variants
            .Select(v => new B2BOfferLineFormModel
            {
                VariantId = v.VariantId,
                Combination = v.Combination,
                AvailableQuantity = v.AvailableQuantity,
                Quantity = 0,
            })
            .ToList(),
    };

    private static B2BOfferFormModel Rehydrate(B2BOfferFormModel form, OfferListingView listing)
    {
        var quantities = form.Lines.ToDictionary(l => l.VariantId, l => l.Quantity);
        form.ListingSlug = listing.Slug;
        form.Lines = listing.Variants
            .Select(v => new B2BOfferLineFormModel
            {
                VariantId = v.VariantId,
                Combination = v.Combination,
                AvailableQuantity = v.AvailableQuantity,
                Quantity = quantities.GetValueOrDefault(v.VariantId),
            })
            .ToList();
        return form;
    }

    private static B2BOfferFormModel BuildCounterForm(B2BNegotiationDetailView negotiation)
    {
        var current = negotiation.CurrentRevision;
        var currentQty = current.Lines.ToDictionary(l => l.VariantCombination, l => l.Quantity);
        return new B2BOfferFormModel
        {
            ListingSlug = negotiation.ListingSlug,
            ProposedUnitPrice = current.UnitPrice,
            ValidityDays = 3,
            Message = null,
            Lines = negotiation.Variants
                .Select(v => new B2BOfferLineFormModel
                {
                    VariantId = v.VariantId,
                    Combination = v.Combination,
                    AvailableQuantity = v.AvailableQuantity,
                    Quantity = currentQty.GetValueOrDefault(v.Combination),
                })
                .ToList(),
        };
    }
}
