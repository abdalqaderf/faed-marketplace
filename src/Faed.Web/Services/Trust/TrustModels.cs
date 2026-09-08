using Faed.Web.Services.Common;

namespace Faed.Web.Services.Trust;

// ---- Review inputs --------------------------------------------------------------

/// <summary>
/// A buyer's request to review a merchant after a completed order. Eligibility (the order
/// is <c>Completed</c>, the reviewer took part, and has not already reviewed it) is enforced
/// server-side.
/// </summary>
public sealed record SubmitReviewInput(
    Guid OrderId,
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
    string ReviewerLabel,
    DateTime CreatedAtUtc);

/// <summary>Aggregate rating for a merchant.</summary>
public sealed record MerchantRatingSummary(int ReviewCount, double AverageRating)
{
    public bool HasReviews => ReviewCount > 0;
}

public sealed record MerchantReviewsView(
    MerchantRatingSummary Summary,
    IReadOnlyList<MerchantReviewView> Reviews);

/// <summary>
/// The merchant-owner view keeps the all-time aggregate separate from the bounded page of
/// review rows, so an established merchant can reach their complete history without loading
/// it all at once.
/// </summary>
public sealed record MerchantReviewHistoryView(
    MerchantRatingSummary Summary,
    PagedResult<MerchantReviewView> Reviews);
