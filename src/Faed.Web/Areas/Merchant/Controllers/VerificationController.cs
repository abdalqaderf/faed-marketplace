using Faed.Application.Common;
using Faed.Application.Merchants;
using Faed.Web.Areas.Merchant.Models;
using Faed.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// Merchant self-service for the verification application. Any authenticated user may
/// start an application (docs/16-PERMISSIONS-MATRIX.md); selling capability is gated
/// separately by the <c>ApprovedMerchant</c> policy.
/// </summary>
[Area("Merchant")]
[Authorize]
public sealed class VerificationController(IMerchantVerificationService verification) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var application = await verification.GetMyApplicationAsync(User.RequireUserId(), cancellationToken);
        return View(new MerchantVerificationPageModel { Application = application });
    }

    [HttpGet]
    public async Task<IActionResult> Apply(CancellationToken cancellationToken)
    {
        var application = await verification.GetMyApplicationAsync(User.RequireUserId(), cancellationToken);
        if (application is { IsEditable: false })
        {
            TempData["StatusMessage"] = "Your application is being reviewed and can no longer be edited.";
            return RedirectToAction(nameof(Index));
        }

        var form = application is null
            ? new MerchantApplicationFormModel()
            : new MerchantApplicationFormModel
            {
                BusinessName = application.BusinessName,
                ContactEmail = application.ContactEmail,
                ContactPhone = application.ContactPhone,
            };

        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Apply(MerchantApplicationFormModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await verification.SaveDraftAsync(
            User.RequireUserId(),
            new MerchantApplicationInput(form.BusinessName, form.ContactEmail, form.ContactPhone),
            cancellationToken);

        if (result.Failed)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(form);
        }

        TempData["StatusMessage"] = "Business details saved. Attach your verification documents, then submit for review.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(VerificationDocumentUploadModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.File is null || model.File.Length == 0)
        {
            TempData["ErrorMessage"] = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? "Choose a PDF, JPG or PNG file to upload.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = model.File.OpenReadStream();
        var result = await verification.AddDocumentAsync(
            User.RequireUserId(),
            new AddVerificationDocumentInput(
                model.DocumentType,
                stream,
                model.File.FileName,
                model.File.ContentType,
                model.File.Length),
            cancellationToken);

        SetOutcome(result, "Document attached.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDocument(Guid id, CancellationToken cancellationToken)
    {
        var result = await verification.RemoveDocumentAsync(User.RequireUserId(), id, cancellationToken);
        SetOutcome(result, "Document removed.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Submit(CancellationToken cancellationToken)
    {
        var result = await verification.SubmitForReviewAsync(User.RequireUserId(), cancellationToken);
        SetOutcome(result, "Application submitted. An administrator will review it shortly.");
        return RedirectToAction(nameof(Index));
    }

    private void SetOutcome(Result result, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
        }
        else
        {
            TempData["ErrorMessage"] = result.Error;
        }
    }
}
