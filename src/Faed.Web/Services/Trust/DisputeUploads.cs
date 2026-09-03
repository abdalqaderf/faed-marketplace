using Microsoft.AspNetCore.Http;

namespace Faed.Web.Services.Trust;

/// <summary>
/// Maps posted <see cref="IFormFile"/>s to <see cref="DisputeEvidenceUpload"/>s. Kept in the
/// service layer so every controller that files or supplements a dispute converts uploads the
/// same way; the dispute service is the single place that validates the bytes
/// (docs/08-SECURITY-AND-PRIVACY.md §4).
/// </summary>
public static class DisputeUploads
{
    public static IReadOnlyList<DisputeEvidenceUpload> From(IEnumerable<IFormFile>? files) =>
        (files ?? [])
            .Where(f => f.Length > 0)
            .Select(f => new DisputeEvidenceUpload(
                f.OpenReadStream(), f.FileName, f.ContentType, f.Length))
            .ToList();
}
