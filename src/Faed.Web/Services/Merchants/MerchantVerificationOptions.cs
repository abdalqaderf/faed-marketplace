namespace Faed.Web.Services.Merchants;

/// <summary>
/// Configurable merchant-verification upload limits. Bound from configuration in the web host;
/// the defaults here are safe and reversible.
/// </summary>
public sealed class MerchantVerificationOptions
{
    public const string SectionName = "MerchantVerification";

    /// <summary>Maximum accepted size for a single verification document.</summary>
    public long MaxDocumentBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Content types accepted for verification documents.</summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
    ];

    /// <summary>File extensions accepted for verification documents (lower-case, leading dot).</summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
    ];

    /// <summary>Maximum number of active documents a single application may hold.</summary>
    public int MaxDocumentsPerApplication { get; set; } = 10;
}
