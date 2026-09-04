using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;
using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// Merchant self-service for the verification application. Any authenticated user may
/// start an application; selling capability is gated
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
    public async Task<IActionResult> UploadDocument(VerificationDocumentUploadModel upload, CancellationToken cancellationToken)
    {
        var userId = User.RequireUserId();

        if (upload.File is { Length: 0 })
        {
            ModelState.AddModelError($"Upload.{nameof(upload.File)}", "The selected file is empty.");
        }

        if (ModelState.IsValid && upload.File is not null)
        {
            await using var stream = upload.File.OpenReadStream();
            var result = await verification.AddDocumentAsync(
                userId,
                new AddVerificationDocumentInput(
                    upload.DocumentType,
                    stream,
                    upload.File.FileName,
                    upload.File.ContentType,
                    upload.File.Length),
                cancellationToken);

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Document attached.";
                return RedirectToAction(nameof(Index));
            }

            // Surface the server-side upload failure against the field it concerns so the
            // merchant sees it inline rather than as one detached banner after a redirect.
            var field = result.ErrorKind == ResultErrorKind.Validation
                        && (result.Error?.Contains("document type", StringComparison.OrdinalIgnoreCase) ?? false)
                ? nameof(upload.DocumentType)
                : nameof(upload.File);
            ModelState.AddModelError($"Upload.{field}", result.Error ?? "The document could not be attached.");
        }

        var application = await verification.GetMyApplicationAsync(userId, cancellationToken);
        return View(nameof(Index), new MerchantVerificationPageModel { Application = application, Upload = upload });
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
