using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One image attached to a listing (docs/04-DOMAIN-MODEL.md §3). Only the private storage
/// object key is kept, never a public URL: the bytes are served through an authorized
/// endpoint so a non-Live listing's photography cannot be reached by guessing a path
/// (docs/08-SECURITY-AND-PRIVACY.md §3-4).
///
/// <see cref="MediaType"/> keeps defect evidence distinguishable from ordinary product
/// photography, which is what lets the buyer-facing pages put a disclosed fault in front of
/// the buyer instead of burying it (docs/01-PRD.md §8).
/// </summary>
public class ListingMedia
{
    public const int MaxAltTextLength = 200;

    private ListingMedia()
    {
    }

    internal ListingMedia(
        ListingMediaType mediaType,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? altText,
        int sortOrder,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        MediaType = mediaType;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        AltText = altText;
        SortOrder = sortOrder;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    public ListingMediaType MediaType { get; private set; }

    /// <summary>Opaque key returned by <c>IFileStorage</c>. Never a publicly reachable URL.</summary>
    public string StorageObjectKey { get; private set; } = null!;

    public string OriginalFileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string? AltText { get; private set; }

    public int SortOrder { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Listing Listing { get; private set; } = null!;
}
