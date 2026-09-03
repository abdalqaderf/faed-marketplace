using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Trust;

// ---- Dispute inputs --------------------------------------------------------------

/// <summary>One evidence file a participant wants to attach to a dispute.</summary>
public sealed record DisputeEvidenceUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long LengthBytes);

/// <summary>
/// A participant's request to open a dispute. The transaction and the raiser's participation
/// are resolved and re-checked server-side; nothing here is trusted straight from the request
/// (docs/08-SECURITY-AND-PRIVACY.md §6, §9).
/// </summary>
public sealed record FileDisputeInput(
    TrustTransactionType TransactionType,
    Guid TransactionId,
    DisputeReasonCode ReasonCode,
    string Description,
    IReadOnlyList<DisputeEvidenceUpload> Evidence);

// ---- Dispute views --------------------------------------------------------------

/// <summary>Which disputes an admin queue should return.</summary>
public enum DisputeQueueFilter
{
    /// <summary>Open or under review — everything that still needs an administrator.</summary>
    Active = 0,
    Open = 1,
    UnderReview = 2,
    Resolved = 3,
    Rejected = 4,
    All = 5,
}

/// <summary>An evidence file as listed on a dispute (never the bytes — those stream from a private endpoint).</summary>
public sealed record DisputeEvidenceView(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc,
    bool UploadedByMe);

/// <summary>A row in a participant's or an administrator's dispute list.</summary>
public sealed record DisputeSummaryView(
    Guid Id,
    DisputeStatus Status,
    DisputeReasonCode ReasonCode,
    TrustTransactionType TransactionType,
    Guid TransactionId,
    string TransactionReference,
    string CounterpartyName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public bool IsActive => Status is DisputeStatus.Open or DisputeStatus.UnderReview;
}

/// <summary>The full picture of one dispute for a participant.</summary>
public sealed record DisputeDetailView(
    Guid Id,
    DisputeStatus Status,
    DisputeReasonCode ReasonCode,
    string Description,
    string? AdminResolution,
    TrustTransactionType TransactionType,
    Guid TransactionId,
    string TransactionReference,
    string? TransactionSlug,
    string RaisedByName,
    bool RaisedByMe,
    string SellingMerchantName,
    string BuyerName,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    bool CanAddEvidence,
    IReadOnlyList<DisputeEvidenceView> Evidence);

/// <summary>The full picture of one dispute for the reviewing administrator.</summary>
public sealed record AdminDisputeDetailView(
    Guid Id,
    DisputeStatus Status,
    DisputeReasonCode ReasonCode,
    string Description,
    string? AdminResolution,
    string? ResolvedByAdminId,
    TrustTransactionType TransactionType,
    Guid TransactionId,
    string TransactionReference,
    string RaisedByName,
    string RaisedByUserId,
    string SellingMerchantName,
    string BuyerName,
    decimal TransactionTotal,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    IReadOnlyList<DisputeEvidenceView> Evidence)
{
    public bool CanStartReview => Status == DisputeStatus.Open;

    /// <summary>An outcome can be recorded only once the dispute is under review
    /// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10).</summary>
    public bool CanClose => Status == DisputeStatus.UnderReview;
}

// ---- Review inputs --------------------------------------------------------------

/// <summary>
/// A buyer's request to review a merchant after a completed transaction. Eligibility (the
/// transaction is <c>Completed</c>, the reviewer took part, and has not already reviewed it)
/// is enforced server-side (docs/03-BUSINESS-RULES.md §13).
/// </summary>
public sealed record SubmitReviewInput(
    TrustTransactionType TransactionType,
    Guid TransactionId,
    int Rating,
    string? Comment);

// ---- Review views --------------------------------------------------------------

/// <summary>Whether the signed-in user may review a given transaction, and why not if not.</summary>
public sealed record ReviewEligibilityView(
    bool CanReview,
    bool AlreadyReviewed,
    ExistingReviewView? ExistingReview,
    string? BlockedReason);

public sealed record ExistingReviewView(int Rating, string? Comment, DateTime CreatedAtUtc);

/// <summary>One review as shown on a merchant storefront or the merchant's own "reviews received" page.</summary>
public sealed record MerchantReviewView(
    int Rating,
    string? Comment,
    TrustTransactionType TransactionType,
    string ReviewerLabel,
    DateTime CreatedAtUtc);

/// <summary>Aggregate rating for a merchant (docs/07-UI-UX-SPEC.md §4 "aggregate trust signals").</summary>
public sealed record MerchantRatingSummary(int ReviewCount, double AverageRating)
{
    public bool HasReviews => ReviewCount > 0;
}

public sealed record MerchantReviewsView(
    MerchantRatingSummary Summary,
    IReadOnlyList<MerchantReviewView> Reviews);
