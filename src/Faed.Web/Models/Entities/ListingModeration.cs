using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// One admin review of one submitted listing version (docs/04-DOMAIN-MODEL.md §5). A row is
/// opened every time a listing enters review — on first submission and again on every
/// material edit — and is resolved in place by the reviewing admin. Rows are never deleted
/// or rewritten, so a merchant's rejection history stays intact (AGENTS.md §8).
/// </summary>
public class ListingModeration
{
    public const int MaxReviewNoteLength = 1000;
    public const int MaxReasonForReviewLength = 500;

    private ListingModeration()
    {
    }

    internal ListingModeration(Guid submittedByMerchantProfileId, string reasonForReview, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        SubmittedByMerchantProfileId = submittedByMerchantProfileId;
        ReasonForReview = reasonForReview.Length <= MaxReasonForReviewLength
            ? reasonForReview
            : reasonForReview[..MaxReasonForReviewLength];
        Status = ListingModerationStatus.Pending;
        SubmittedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    public Guid SubmittedByMerchantProfileId { get; private set; }

    /// <summary>Why this version needs review — first submission, or which material field changed.</summary>
    public string ReasonForReview { get; private set; } = null!;

    public ListingModerationStatus Status { get; private set; }

    public string? ReviewedByAdminId { get; private set; }

    public string? ReviewNote { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? ReviewedAtUtc { get; private set; }

    public bool IsPending => Status == ListingModerationStatus.Pending;

    /// <summary>
    /// Records a further material change made while this review was still open, so the
    /// reviewing admin sees everything the merchant altered rather than only the first edit.
    /// </summary>
    internal void AppendReason(string reason)
    {
        if (Status != ListingModerationStatus.Pending)
        {
            return;
        }

        // Compare against each "; "-separated entry already recorded, not the combined text
        // as a whole: a plain substring check would silently drop a genuinely new reason that
        // happens to be textually contained in the accumulated string so far (for example
        // "grade changed" inside "condition grade changed").
        var segments = ReasonForReview.Split("; ", StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, reason, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var combined = $"{ReasonForReview}; {reason}";
        ReasonForReview = combined.Length <= MaxReasonForReviewLength
            ? combined
            : combined[..MaxReasonForReviewLength];
    }

    internal void Resolve(ListingModerationStatus outcome, string adminUserId, string? reviewNote, DateTime nowUtc)
    {
        if (Status != ListingModerationStatus.Pending)
        {
            throw new DomainException("This moderation record has already been decided.");
        }

        if (outcome == ListingModerationStatus.Pending)
        {
            throw new DomainException("A moderation decision must approve or reject.");
        }

        Status = outcome;
        ReviewedByAdminId = adminUserId;
        ReviewNote = reviewNote;
        ReviewedAtUtc = nowUtc;
    }
}
