using Faed.Web.Services.Common;
using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Merchants;

/// <summary>
/// Server-side validation of an uploaded verification document
/// (docs/08-SECURITY-AND-PRIVACY.md §3-4). The client-supplied file name is never trusted
/// for storage; the client-supplied content type and extension are trusted only after
/// they agree with each other <em>and</em> with the file's actual byte signature.
/// </summary>
public static class VerificationDocumentValidator
{
    /// <summary>Accepted content types mapped to the extensions that may accompany them.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> ContentTypeExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
        };

    /// <summary>Number of leading bytes needed to recognise every supported signature.</summary>
    public const int SignatureProbeBytes = 12;

    /// <summary>Validates the declared metadata (type, size, content type / extension pairing).</summary>
    public static Result ValidateMetadata(AddVerificationDocumentInput input, MerchantVerificationOptions options)
    {
        if (!Enum.IsDefined(input.DocumentType))
        {
            return Result.Validation("Choose a valid document type.");
        }

        if (input.LengthBytes <= 0)
        {
            return Result.Validation("The file is empty.");
        }

        if (input.LengthBytes > options.MaxDocumentBytes)
        {
            return Result.Validation($"The file exceeds the {MaxMegabytes(options)} MB limit.");
        }

        var contentType = (input.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!options.AllowedContentTypes.Contains(contentType) || !ContentTypeExtensions.TryGetValue(contentType, out var extensions))
        {
            return Result.Validation("Only PDF, JPG and PNG documents are accepted.");
        }

        var extension = Path.GetExtension(input.OriginalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !extensions.Contains(extension) || !options.AllowedExtensions.Contains(extension))
        {
            return Result.Validation("The file extension does not match its type. Use a genuine PDF, JPG or PNG.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Confirms the first bytes of the file match a real PDF, JPEG or PNG for the declared
    /// content type, defeating renamed or polyglot uploads.
    /// </summary>
    public static Result ValidateSignature(ReadOnlySpan<byte> header, string contentType)
    {
        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var matches = normalized switch
        {
            "application/pdf" => StartsWith(header, "%PDF-"u8),
            "image/png" => StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            _ => false,
        };

        return matches
            ? Result.Success()
            : Result.Validation("The file content is not a valid PDF, JPG or PNG.");
    }

    public static double MaxMegabytes(MerchantVerificationOptions options) =>
        Math.Round(options.MaxDocumentBytes / (1024d * 1024d), 1);

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}
