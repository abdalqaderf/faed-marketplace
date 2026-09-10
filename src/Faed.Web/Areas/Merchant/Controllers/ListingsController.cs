using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// The merchant's listing workspace: the one-page create/edit form (CORE.md §3.2) and the two
/// lifecycle actions a merchant sees — Pause and Delete. Gated by <c>RegisteredMerchant</c> so
/// a listing can be built before verification is approved; the service re-checks ownership on
/// every call and enforces the publish gate (verified, subscribed, under quota) on submission.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.RegisteredMerchant)]
public sealed class ListingsController(IMerchantListingService listings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        MerchantListingFilter filter = MerchantListingFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var items = await listings.GetMyListingsAsync(User.RequireUserId(), filter, page, cancellationToken);
        return View(new ListingListPageModel { Filter = filter, Items = items });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        return View("Form", new ListingFormPageModel
        {
            Form = ListingFormModel.ForNewListing(),
            ReferenceData = referenceData,
        });
    }

    [HttpPost]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public Task<IActionResult> Create(
        [Bind(Prefix = "Form")] ListingFormModel form, CancellationToken cancellationToken) =>
        SaveAsync(null, form, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var listing = await listings.GetMyListingAsync(User.RequireUserId(), id, cancellationToken);
        if (listing is null)
        {
            return NotFound();
        }

        // Inline, immediate validation: whatever currently stops this listing publishing is
        // shown next to the field that fixes it, every time the page is opened.
        foreach (var blocker in listing.SubmissionBlockers)
        {
            ModelState.AddModelError($"Form.{blocker.Field}", blocker.Message);
        }

        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        return View("Form", new ListingFormPageModel
        {
            Form = ListingFormModel.FromDetail(listing),
            ReferenceData = referenceData,
            Listing = listing,
        });
    }

    [HttpPost]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public Task<IActionResult> Edit(
        Guid id, [Bind(Prefix = "Form")] ListingFormModel form, CancellationToken cancellationToken) =>
        SaveAsync(id, form, cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken) =>
        await AfterLifecycleAsync(
            id, await listings.HideAsync(User.RequireUserId(), id, cancellationToken),
            "Paused. It's hidden from the shop and a quota slot is free.");

    [HttpPost]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) =>
        await AfterLifecycleAsync(
            id, await listings.RestoreAsync(User.RequireUserId(), id, cancellationToken),
            "Back in the shop.");

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await listings.ArchiveAsync(User.RequireUserId(), id, cancellationToken);
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "Deleted.";
            return RedirectToAction(nameof(Index));
        }

        return await AfterLifecycleAsync(id, result, string.Empty);
    }

    // ---- Internals ------------------------------------------------------------------

    private async Task<IActionResult> SaveAsync(Guid? id, ListingFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RenderFormAsync(id, form, cancellationToken);
        }

        var openedStreams = new List<Stream>();
        try
        {
            var submission = BuildSubmission(form, openedStreams);
            var result = await listings.SaveListingAsync(User.RequireUserId(), id, submission, cancellationToken);

            if (result.Failed)
            {
                if (result.ErrorKind == ResultErrorKind.NotFound)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Error!);
                return await RenderFormAsync(id, form, cancellationToken);
            }

            var outcome = result.Value;
            TempData["StatusMessage"] = outcome switch
            {
                { Published: true, Status: ListingStatus.PendingReview } =>
                    "Published — it's under review. We'll let you know when it goes live.",
                { Published: true } => "Saved.",
                { GateMessage: { } gate } => gate,
                _ => "Saved as a draft. Fix the highlighted items to publish.",
            };

            // Always land on the edit view (PRG): it re-derives and shows any remaining
            // blockers inline, and a published listing shows its "under review" state there.
            return RedirectToAction(nameof(Edit), new { id = outcome.ListingId });
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private static ListingFormSubmission BuildSubmission(ListingFormModel form, List<Stream> openedStreams)
    {
        IncomingPhoto Open(IFormFile file)
        {
            var stream = file.OpenReadStream();
            openedStreams.Add(stream);
            return new IncomingPhoto(stream, file.FileName, file.ContentType, file.Length);
        }

        var productPhotos = form.Photos
            .Where(f => f.Length > 0)
            .Select(Open)
            .ToList();

        var defectPhotos = form.DefectPhoto is { Length: > 0 } defect
            ? new List<IncomingPhoto> { Open(defect) }
            : [];

        IncomingEvidence? evidence = null;
        if (form.OriginalPrice is not null)
        {
            if (form.OriginalPriceEvidence is { Length: > 0 } evidenceFile)
            {
                evidence = new IncomingEvidence(
                    ReferencePriceEvidenceType.Photo, form.OriginalPriceLink, Open(evidenceFile));
            }
            else if (!string.IsNullOrWhiteSpace(form.OriginalPriceLink))
            {
                evidence = new IncomingEvidence(ReferencePriceEvidenceType.Link, form.OriginalPriceLink, null);
            }
        }

        return new ListingFormSubmission(
            form.Title.Trim(),
            string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
            form.CategoryId!.Value,
            form.Condition!.Value,
            form.ExtraReasonIds,
            form.WarrantyType,
            form.WarrantyMonths,
            form.Price,
            form.OriginalPrice,
            form.Quantity,
            productPhotos,
            defectPhotos,
            form.RemovePhotoIds,
            evidence);
    }

    private async Task<IActionResult> RenderFormAsync(Guid? id, ListingFormModel form, CancellationToken cancellationToken)
    {
        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        var listing = id is { } listingId
            ? await listings.GetMyListingAsync(User.RequireUserId(), listingId, cancellationToken)
            : null;

        return View("Form", new ListingFormPageModel
        {
            Form = form,
            ReferenceData = referenceData,
            Listing = listing,
        });
    }

    private async Task<IActionResult> AfterLifecycleAsync(Guid id, Result result, string successMessage)
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

        return RedirectToAction(nameof(Edit), new { id });
    }
}
