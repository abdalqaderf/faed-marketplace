using Faed.Web.Authorization;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Controllers;

/// <summary>
/// Streams a private dispute evidence file. There is no public URL to the underlying object
/// key: the dispute service resolves every request and serves the bytes only to the
/// dispute's participants and to administrators (an administrator's access is audited)
/// (docs/08-SECURITY-AND-PRIVACY.md §3-4, docs/17-DATA-INVARIANTS.md "Private document
/// authorization is never based on knowing the storage object key"). Evidence is never
/// public, so anonymous requests are challenged rather than resolved.
/// </summary>
[Authorize]
public sealed class DisputeEvidenceController(IDisputeService disputes) : Controller
{
    [HttpGet("dispute-evidence/{id:guid}")]
    public async Task<IActionResult> Show(Guid id, CancellationToken cancellationToken)
    {
        var result = await disputes.OpenEvidenceAsync(User.RequireUserId(), id, cancellationToken);
        if (result.Failed)
        {
            return result.ErrorKind == ResultErrorKind.NotFound ? NotFound() : Forbid();
        }

        // Served as an attachment, never inline: this is participant-supplied content
        // (docs/08-SECURITY-AND-PRIVACY.md §3-4, matching the verification-document endpoint).
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.OriginalFileName);
    }
}
