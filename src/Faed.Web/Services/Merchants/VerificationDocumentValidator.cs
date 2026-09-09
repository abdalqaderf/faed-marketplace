using Faed.Web.Services.Common;

namespace Faed.Web.Services.Merchants;

/// <summary>
/// Server-side validation of an uploaded verification document. The client-supplied file name
/// is never trusted for storage; the client-supplied content type and extension are trusted
/// only after they agree with each other and with the file's magic bytes.
/// </summary>
public static class VerificationDocumentValidator
{
    private static readonly IReadOnlyDictionary<string, string[]> ContentTypeExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = [".pdf"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
        };

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Number of leading bytes needed to recognise every supported signature.</summary>
    public const int SignatureProbeBytes = 8;

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
        if (!options.AllowedContentTypes.Contains(contentType)
            || !ContentTypeExtensions.TryGetValue(contentType, out var extensions))
        {
            return Result.Validation("Only PDF, JPG and PNG documents are accepted.");
        }

        var extension = Path.GetExtension(input.OriginalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension)
            || !extensions.Contains(extension)
            || !options.AllowedExtensions.Contains(extension))
        {
            return Result.Validation("The file extension does not match its type. Use a genuine PDF, JPG or PNG.");
        }

        return Result.Success();
    }

    /// <summary>Checks the declared content type against the file's magic bytes.</summary>
    public static Result ValidatePayload(ReadOnlySpan<byte> content, string contentType)
    {
        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var matches = normalized switch
        {
            "application/pdf" => StartsWith(content, PdfSignature),
            "image/png" => StartsWith(content, PngSignature),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            _ => false,
        };

        return matches
            ? Result.Success()
            : Result.Validation("The file content is not a valid PDF, JPG or PNG.");
    }

    public static double MaxMegabytes(MerchantVerificationOptions options) =>
        Math.Round(options.MaxDocumentBytes / (1024d * 1024d), 1);

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
}
