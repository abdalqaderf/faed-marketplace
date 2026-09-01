using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// Merchant listing management: create, edit variants/media/evidence, and submit for
/// moderation (tasks/TASK-004-LISTINGS-AND-INVENTORY.md). Gated by the
/// <c>ApprovedMerchant</c> policy; the service layer re-checks ownership on every call.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class ListingsController(IMerchantListingService listings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        MerchantListingFilter filter = MerchantListingFilter.All, CancellationToken cancellationToken = default)
    {
        var items = await listings.GetMyListingsAsync(User.RequireUserId(), filter, cancellationToken);
        return View(new ListingListPageModel { Filter = filter, Items = items });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        ViewData["ReferenceData"] = referenceData;
        return View(new ListingFormModel { AllowB2C = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create(ListingFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReferenceData"] = await listings.GetReferenceDataAsync(cancellationToken);
            return View(form);
        }

        var result = await listings.CreateAsync(User.RequireUserId(), form.ToInput(), cancellationToken);
        if (result.Failed)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            ViewData["ReferenceData"] = await listings.GetReferenceDataAsync(cancellationToken);
            return View(form);
        }

        TempData["StatusMessage"] = "Listing created as a draft. Add variants and photos, then submit for review.";
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var listing = await listings.GetMyListingAsync(User.RequireUserId(), id, cancellationToken);
        if (listing is null)
        {
            return NotFound();
        }

        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        return View("Workspace", new ListingWorkspacePageModel
        {
            Listing = listing,
            ReferenceData = referenceData,
            Form = ListingFormModel.FromDetail(listing),
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateDetails(Guid id, ListingFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { Form = form });
        }

        var result = await listings.UpdateDetailsAsync(User.RequireUserId(), id, form.ToInput(), cancellationToken);
        return await AfterMutationAsync(id, result, "Listing details saved.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> AddOption(Guid id, AddOptionModel addOption, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { AddOption = addOption });
        }

        var result = await listings.AddOptionAsync(User.RequireUserId(), id, addOption.Name, cancellationToken);
        return await AfterMutationAsync(id, result, $"Option {addOption.Name} added.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveOption(Guid id, Guid optionId, CancellationToken cancellationToken)
    {
        var result = await listings.RemoveOptionAsync(User.RequireUserId(), id, optionId, cancellationToken);
        return await AfterMutationAsync(id, result, "Option removed.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> AddOptionValue(Guid id, AddOptionValueModel addOptionValue, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { AddOptionValue = addOptionValue });
        }

        var result = await listings.AddOptionValueAsync(
            User.RequireUserId(), id, addOptionValue.OptionId, addOptionValue.Value, cancellationToken);
        return await AfterMutationAsync(id, result, $"{addOptionValue.Value} added.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveOptionValue(
        Guid id, Guid optionId, Guid optionValueId, CancellationToken cancellationToken)
    {
        var result = await listings.RemoveOptionValueAsync(
            User.RequireUserId(), id, optionId, optionValueId, cancellationToken);
        return await AfterMutationAsync(id, result, "Value removed.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> AddVariant(Guid id, AddVariantModel addVariant, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { AddVariant = addVariant });
        }

        var result = await listings.AddVariantAsync(
            User.RequireUserId(), id,
            new AddVariantInput(addVariant.Sku, addVariant.OptionValueIds, addVariant.InitialQuantity),
            cancellationToken);
        return await AfterMutationAsync(id, result, $"Variant {addVariant.Sku} added.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveVariant(Guid id, Guid variantId, CancellationToken cancellationToken)
    {
        var result = await listings.RemoveVariantAsync(User.RequireUserId(), id, variantId, cancellationToken);
        return await AfterMutationAsync(id, result, "Variant removed.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleVariant(
        Guid id, Guid variantId, bool isActive, CancellationToken cancellationToken)
    {
        var result = await listings.SetVariantActiveAsync(
            User.RequireUserId(), id, variantId, isActive, cancellationToken);
        return await AfterMutationAsync(
            id, result, isActive ? "Variant reactivated." : "Variant deactivated.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(Guid id, UploadImageModel uploadImage, CancellationToken cancellationToken)
    {
        if (uploadImage.File is { Length: 0 })
        {
            ModelState.AddModelError($"{nameof(uploadImage)}.{nameof(uploadImage.File)}", "The selected file is empty.");
        }

        if (!ModelState.IsValid || uploadImage.File is null)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { UploadImage = uploadImage });
        }

        await using var stream = uploadImage.File.OpenReadStream();
        var result = await listings.AddImageAsync(
            User.RequireUserId(), id,
            new AddListingImageInput(
                uploadImage.MediaType, stream, uploadImage.File.FileName, uploadImage.File.ContentType,
                uploadImage.File.Length, uploadImage.AltText),
            cancellationToken);

        return await AfterMutationAsync(id, result, "Image added.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveImage(Guid id, Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await listings.RemoveImageAsync(User.RequireUserId(), id, mediaId, cancellationToken);
        return await AfterMutationAsync(id, result, "Image removed.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> AddEvidence(Guid id, AddEvidenceModel addEvidence, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws with { AddEvidence = addEvidence });
        }

        Stream? content = null;
        var openedFile = addEvidence.File is { Length: > 0 } ? addEvidence.File.OpenReadStream() : null;
        try
        {
            content = openedFile;
            var result = await listings.AddReferencePriceEvidenceAsync(
                User.RequireUserId(), id,
                new AddReferencePriceEvidenceInput(
                    addEvidence.EvidenceType,
                    addEvidence.ReferenceUrl,
                    addEvidence.Note,
                    content,
                    addEvidence.File?.FileName,
                    addEvidence.File?.ContentType,
                    addEvidence.File?.Length ?? 0),
                cancellationToken);

            return await AfterMutationAsync(id, result, "Evidence added.", cancellationToken);
        }
        finally
        {
            if (openedFile is not null)
            {
                await openedFile.DisposeAsync();
            }
        }
    }

    [HttpPost]
    public async Task<IActionResult> RemoveEvidence(Guid id, Guid evidenceId, CancellationToken cancellationToken)
    {
        var result = await listings.RemoveReferencePriceEvidenceAsync(
            User.RequireUserId(), id, evidenceId, cancellationToken);
        return await AfterMutationAsync(id, result, "Evidence removed.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await listings.SubmitForReviewAsync(User.RequireUserId(), id, cancellationToken);
        return await AfterMutationAsync(
            id, result, "Submitted for review. An administrator will look at it shortly.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken)
    {
        var result = await listings.HideAsync(User.RequireUserId(), id, cancellationToken);
        return await AfterMutationAsync(id, result, "Listing hidden from the public marketplace.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await listings.RestoreAsync(User.RequireUserId(), id, cancellationToken);
        return await AfterMutationAsync(id, result, "Listing republished.", cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await listings.ArchiveAsync(User.RequireUserId(), id, cancellationToken);
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "Listing archived.";
            return RedirectToAction(nameof(Index));
        }

        return await AfterMutationAsync(id, result, string.Empty, cancellationToken);
    }

    private async Task<IActionResult> AfterMutationAsync(
        Guid id, Result result, string successMessage, CancellationToken cancellationToken)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (result.ErrorKind == ResultErrorKind.NotFound)
        {
            return NotFound();
        }

        ModelState.AddModelError(string.Empty, result.Error!);
        return await RedisplayWorkspaceAsync(id, cancellationToken, ws => ws);
    }

    private async Task<IActionResult> RedisplayWorkspaceAsync(
        Guid id, CancellationToken cancellationToken, Func<ListingWorkspacePageModel, ListingWorkspacePageModel> adjust)
    {
        var listing = await listings.GetMyListingAsync(User.RequireUserId(), id, cancellationToken);
        if (listing is null)
        {
            return NotFound();
        }

        var referenceData = await listings.GetReferenceDataAsync(cancellationToken);
        var page = adjust(new ListingWorkspacePageModel
        {
            Listing = listing,
            ReferenceData = referenceData,
            Form = ListingFormModel.FromDetail(listing),
        });

        return View("Workspace", page);
    }
}
