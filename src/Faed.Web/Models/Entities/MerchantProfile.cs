using Faed.Web.Models.Enums;
using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A merchant business account. 1:1 with an Identity user. Selling is gated on
/// <see cref="VerificationStatus"/> being <see cref="MerchantVerificationStatus.Approved"/> —
/// the Merchant role alone is never sufficient (AGENTS.md §3, docs/03-BUSINESS-RULES.md §1).
/// </summary>
public class MerchantProfile
{
    /// <summary>Maximum length of a rejection or suspension reason. Mirrored by the EF mapping.</summary>
    public const int MaxDecisionReasonLength = 1000;

    private readonly List<MerchantVerificationDocument> _documents = [];

    private MerchantProfile()
    {
    }

    public MerchantProfile(string userId, string businessName, string publicSlug, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        BusinessName = businessName;
        PublicSlug = publicSlug;
        VerificationStatus = MerchantVerificationStatus.Draft;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>FK to <c>AspNetUsers.Id</c>. No navigation keeps the entity independent of the Identity object graph.</summary>
    public string UserId { get; private set; } = null!;

    public string BusinessName { get; private set; } = null!;

    /// <summary>Human-readable public storefront identifier. Not an authorization key (docs/06-ARCHITECTURE.md §12).</summary>
    public string PublicSlug { get; private set; } = null!;

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public MerchantVerificationStatus VerificationStatus { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public string? ReviewedByAdminId { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c>). Protects against two
    /// administrators making competing verification decisions on the same application.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<MerchantVerificationDocument> Documents => _documents.AsReadOnly();

    public IEnumerable<MerchantVerificationDocument> ActiveDocuments => _documents.Where(d => d.IsActive);

    /// <summary>True when the merchant is allowed to act as a seller.</summary>
    public bool CanSell => VerificationStatus == MerchantVerificationStatus.Approved;

    /// <summary>The merchant can still edit the application (Draft or Rejected).</summary>
    public bool IsEditable =>
        VerificationStatus is MerchantVerificationStatus.Draft or MerchantVerificationStatus.Rejected;

    public void UpdateBusinessDetails(string businessName, string? contactEmail, string? contactPhone, DateTime nowUtc)
    {
        RequireEditable();
        BusinessName = businessName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Touch(nowUtc);
    }

    public MerchantVerificationDocument AddDocument(
        MerchantVerificationDocumentType type,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTime nowUtc)
    {
        RequireEditable();
        var document = new MerchantVerificationDocument(
            type, storageObjectKey, originalFileName, contentType, sizeBytes, nowUtc);
        _documents.Add(document);
        Touch(nowUtc);
        return document;
    }

    public void RemoveDocument(Guid documentId, DateTime nowUtc)
    {
        RequireEditable();
        var document = _documents.SingleOrDefault(d => d.Id == documentId && d.IsActive)
            ?? throw new DomainException("Document not found on this merchant application.");
        document.Deactivate();
        Touch(nowUtc);
    }

    /// <summary>Merchant submits the application for admin review.</summary>
    public void SubmitForReview(DateTime nowUtc)
    {
        if (!IsEditable)
        {
            throw new DomainException(
                $"A merchant application in status {VerificationStatus} cannot be submitted for review.");
        }

        if (!ActiveDocuments.Any())
        {
            throw new DomainException("At least one verification document is required before submission.");
        }

        VerificationStatus = MerchantVerificationStatus.PendingReview;
        SubmittedAtUtc = nowUtc;
        ReviewedAtUtc = null;
        ReviewedByAdminId = null;
        RejectionReason = null;
        Touch(nowUtc);
    }

    public void Approve(string adminUserId, DateTime nowUtc)
    {
        RequirePending();
        VerificationStatus = MerchantVerificationStatus.Approved;
        StampReview(adminUserId, nowUtc);
        RejectionReason = null;
    }

    public void Reject(string adminUserId, string reason, DateTime nowUtc)
    {
        RequirePending();
        var normalized = NormalizeReason(reason, "rejection");
        VerificationStatus = MerchantVerificationStatus.Rejected;
        StampReview(adminUserId, nowUtc);
        RejectionReason = normalized;
    }

    public void Suspend(string adminUserId, string reason, DateTime nowUtc)
    {
        if (VerificationStatus != MerchantVerificationStatus.Approved)
        {
            throw new DomainException("Only an approved merchant can be suspended.");
        }

        var normalized = NormalizeReason(reason, "suspension");
        VerificationStatus = MerchantVerificationStatus.Suspended;
        StampReview(adminUserId, nowUtc);
        RejectionReason = normalized;
    }

    private static string NormalizeReason(string reason, string kind)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException($"A {kind} reason is required.");
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > MaxDecisionReasonLength)
        {
            throw new DomainException($"The {kind} reason must be {MaxDecisionReasonLength} characters or fewer.");
        }

        return trimmed;
    }

    public void Reinstate(string adminUserId, DateTime nowUtc)
    {
        if (VerificationStatus != MerchantVerificationStatus.Suspended)
        {
            throw new DomainException("Only a suspended merchant can be reinstated.");
        }

        VerificationStatus = MerchantVerificationStatus.Approved;
        StampReview(adminUserId, nowUtc);
        RejectionReason = null;
    }

    private void RequirePending()
    {
        if (VerificationStatus != MerchantVerificationStatus.PendingReview)
        {
            throw new DomainException(
                $"A merchant application in status {VerificationStatus} is not awaiting review.");
        }
    }

    private void RequireEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException(
                $"A merchant application in status {VerificationStatus} can no longer be edited.");
        }
    }

    private void StampReview(string adminUserId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new DomainException("An admin user id is required to record a verification decision.");
        }

        ReviewedByAdminId = adminUserId;
        ReviewedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;
}
