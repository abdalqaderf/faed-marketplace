using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;

namespace Faed.Web.Services.Listings;

/// <summary>
/// Server-side validation of an uploaded listing image or reference-price evidence file
/// (docs/08-SECURITY-AND-PRIVACY.md §4). The client-supplied file name, extension and
/// content type are never trusted on their own: they must agree with each other and with
/// the bytes.
///
/// Structural inspection is delegated to
/// <see cref="VerificationDocumentValidator.ValidatePayload"/>, which already walks JPEG,
/// PNG and PDF fail-closed (docs/adr/0007-VERIFICATION-UPLOAD-INSPECTION.md). Listing
/// uploads face the same threat as verification uploads, so they get the same scanner
/// rather than a second, weaker one.
/// </summary>
public static class ListingImageValidator
{
    /// <summary>Photography accepted for listing media. PDFs are not shown to buyers.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ImageContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
        };

    /// <summary>Reference-price evidence may also be a scanned invoice or catalogue page.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> EvidenceContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["application/pdf"] = [".pdf"],
        };

    public static Result ValidateMetadata(
        string? originalFileName,
        string? contentType,
        long lengthBytes,
        long maxBytes,
        IReadOnlyDictionary<string, string[]> accepted)
    {
        var accepts = Describe(accepted);

        if (lengthBytes <= 0)
        {
            return Result.Validation("The file is empty.");
        }

        if (lengthBytes > maxBytes)
        {
            return Result.Validation($"The file exceeds the {Megabytes(maxBytes)} MB limit.");
        }

        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!accepted.TryGetValue(normalized, out var extensions))
        {
            return Result.Validation($"Only {accepts} files are accepted.");
        }

        var extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension))
        {
            return Result.Validation($"The file extension does not match its type. Use a genuine {accepts} file.");
        }

        return Result.Success();
    }

    /// <summary>Fail-closed structural inspection of the complete buffered upload.</summary>
    public static Result ValidatePayload(ReadOnlySpan<byte> content, string contentType) =>
        VerificationDocumentValidator.ValidatePayload(content, contentType);

    public static double Megabytes(long bytes) => Math.Round(bytes / (1024d * 1024d), 1);

    private static string Describe(IReadOnlyDictionary<string, string[]> accepted) =>
        string.Join(" and ", accepted.Keys
            .Select(key => key switch
            {
                "image/jpeg" => "JPG",
                "image/png" => "PNG",
                "application/pdf" => "PDF",
                _ => key,
            })
            .Order(StringComparer.Ordinal));
}
