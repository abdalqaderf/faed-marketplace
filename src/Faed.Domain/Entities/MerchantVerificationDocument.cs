using Faed.Domain.Enums;

namespace Faed.Domain.Entities;

/// <summary>
/// A single private business-verification file attached to a <see cref="MerchantProfile"/>.
/// Only a protected storage object key and metadata are kept; a public URL is never stored
/// (docs/04-DOMAIN-MODEL.md §1, docs/08-SECURITY-AND-PRIVACY.md §3).
/// </summary>
public class MerchantVerificationDocument
{
    private MerchantVerificationDocument()
    {
    }

    public MerchantVerificationDocument(
        MerchantVerificationDocumentType documentType,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTime uploadedAtUtc)
    {
        Id = Guid.CreateVersion7();
        DocumentType = documentType;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedAtUtc = uploadedAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid MerchantProfileId { get; private set; }

    public MerchantVerificationDocumentType DocumentType { get; private set; }

    /// <summary>Opaque key returned by <c>IFileStorage</c>. Never a publicly reachable URL.</summary>
    public string StorageObjectKey { get; private set; } = null!;

    public string OriginalFileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>Soft-removed documents are retained for audit history rather than deleted.</summary>
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}
