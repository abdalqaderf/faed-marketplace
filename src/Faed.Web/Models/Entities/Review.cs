using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A rating and comment a buyer leaves for a merchant after a completed order. Eligibility
/// (the order is <c>Completed</c>, the reviewer took part, and they have not already reviewed
/// it) is enforced by the review service, and the "one review per order" rule is also a
/// filtered unique index on <see cref="OrderId"/>.
/// </summary>
public class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 2000;

    private Review()
    {
    }

    public Review(
        Guid reviewedMerchantProfileId,
        string reviewerUserId,
        Guid orderId,
        int rating,
        string? comment,
        DateTime nowUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("A review must reference the order it is about.");
        }

        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            throw new DomainException("A review needs the user who wrote it.");
        }

        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException($"A rating must be between {MinRating} and {MaxRating}.");
        }

        Id = Guid.CreateVersion7();
        ReviewedMerchantProfileId = reviewedMerchantProfileId;
        ReviewerUserId = reviewerUserId;
        OrderId = orderId;
        Rating = rating;
        Comment = NormalizeComment(comment);
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The merchant being reviewed — the selling merchant of the order.</summary>
    public Guid ReviewedMerchantProfileId { get; private set; }

    /// <summary>The Identity user id of the reviewer — the buyer.</summary>
    public string ReviewerUserId { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();
        if (trimmed.Length > MaxCommentLength)
        {
            throw new DomainException($"A review comment must be {MaxCommentLength} characters or fewer.");
        }

        return trimmed;
    }
}
