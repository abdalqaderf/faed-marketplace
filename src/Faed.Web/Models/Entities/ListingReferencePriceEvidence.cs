using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// Provenance for a listing's reference price (docs/03-BUSINESS-RULES.md §4,
/// docs/04-DOMAIN-MODEL.md §3). A merchant cannot present a reference price as an
/// untrusted marketing number: a listing that claims one must carry at least one of these
/// before it can be submitted for review, and the reviewing admin sees them all.
///
/// Faed deliberately does not encode a minimum discount percentage — the MVP relies on
/// manual review of this evidence.
/// </summary>
public class ListingReferencePriceEvidence
{
    public const int MaxReferenceUrlLength = 2000;
    public const int MaxNoteLength = 1000;

    private ListingReferencePriceEvidence()
    {
    }

    internal ListingReferencePriceEvidence(
        ReferencePriceEvidenceType evidenceType,
        string? referenceUrl,
        string? storageObjectKey,
        string? originalFileName,
        string? contentType,
        string? note,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        EvidenceType = evidenceType;
        ReferenceUrl = referenceUrl;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Note = note;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    public ReferencePriceEvidenceType EvidenceType { get; private set; }

    public string? ReferenceUrl { get; private set; }

    /// <summary>Opaque key for an uploaded invoice or catalogue page, when one was attached.</summary>
    public string? StorageObjectKey { get; private set; }

    public string? OriginalFileName { get; private set; }

    public string? ContentType { get; private set; }

    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Listing Listing { get; private set; } = null!;
}
