using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One file attached to a <see cref="Dispute"/> as evidence. Only a protected object key and its metadata are stored;
/// there is no public URL to the bytes, and the dispute service streams them only to the
/// dispute's participants and to administrators
/// </summary>
public class DisputeEvidence
{
    public const int MaxOriginalFileNameLength = 260;
    public const int MaxContentTypeLength = 100;

    private DisputeEvidence()
    {
    }

    internal DisputeEvidence(
        string uploadedByUserId,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(uploadedByUserId))
        {
            throw new DomainException("Evidence needs the user who uploaded it.");
        }

        if (string.IsNullOrWhiteSpace(storageObjectKey))
        {
            throw new DomainException("Evidence needs a stored object key.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("An evidence file cannot be empty.");
        }

        Id = Guid.CreateVersion7();
        UploadedByUserId = uploadedByUserId;
        StorageObjectKey = storageObjectKey;
        OriginalFileName = Truncate(originalFileName, MaxOriginalFileNameLength, "evidence");
        ContentType = Truncate(contentType, MaxContentTypeLength, "application/octet-stream");
        SizeBytes = sizeBytes;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid DisputeId { get; private set; }

    /// <summary>The Identity user id of whoever uploaded the file (a participant or an administrator).</summary>
    public string UploadedByUserId { get; private set; } = null!;

    /// <summary>Opaque key into private object storage. Never rendered to a client.</summary>
    public string StorageObjectKey { get; private set; } = null!;

    public string OriginalFileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private static string Truncate(string? value, int maxLength, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return fallback;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
