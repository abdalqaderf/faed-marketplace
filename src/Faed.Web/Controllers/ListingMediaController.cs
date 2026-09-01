using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Controllers;

/// <summary>
/// Serves listing photography and reference-price evidence from private object storage.
/// There is no public URL to the underlying object key: every request is resolved here so a
/// non-Live listing's images cannot be reached by guessing a path
/// (docs/08-SECURITY-AND-PRIVACY.md §3, tasks/TASK-004-LISTINGS-AND-INVENTORY.md "Public
/// cannot see non-Live data"). Anonymous requests are allowed for photos of a Live listing —
/// that is public marketplace content — and the service itself checks ownership for anything
/// not yet (or no longer) public; evidence files are never public.
/// </summary>
public sealed class ListingMediaController(IListingMediaService media) : Controller
{
    [HttpGet("listing-images/{id:guid}")]
    public async Task<IActionResult> Show(Guid id, CancellationToken cancellationToken)
    {
        var result = await media.OpenImageAsync(User.GetUserId(), id, cancellationToken);
        if (result.Failed)
        {
            return result.ErrorKind == ResultErrorKind.NotFound ? NotFound() : Forbid();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(result.Value.Content, result.Value.ContentType);
    }

    /// <summary>
    /// Streams a reference-price evidence file (an uploaded invoice or catalogue page) to
    /// the owning merchant or an admin. Never public, so anonymous requests are challenged
    /// rather than resolved by the service (AGENTS.md §8 "the reviewing admin sees them all").
    /// </summary>
    [Authorize]
    [HttpGet("listing-evidence/{id:guid}")]
    public async Task<IActionResult> ShowEvidence(Guid id, CancellationToken cancellationToken)
    {
        var result = await media.OpenReferencePriceEvidenceAsync(User.GetUserId(), id, cancellationToken);
        if (result.Failed)
        {
            return result.ErrorKind == ResultErrorKind.NotFound ? NotFound() : Forbid();
        }

        // Served as an attachment, never inline: this may be a merchant-supplied document
        // (docs/08-SECURITY-AND-PRIVACY.md §3-4, matching the verification-document endpoint).
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.OriginalFileName);
    }
}
