using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A rating and comment a buyer leaves for a merchant after a completed transaction
/// (docs/03-BUSINESS-RULES.md §13, docs/04-DOMAIN-MODEL.md §9, docs/17-DATA-INVARIANTS.md
/// "Review"). A review references exactly one completed transaction context — a B2C
/// <see cref="Order"/> or a B2B <see cref="B2BDeal"/> — enforced by a database check
/// constraint. Eligibility (the transaction is <c>Completed</c>, the reviewer took part, and
/// they have not already reviewed it) is enforced by the review service, and the
/// "one review per transaction" rule is also a filtered unique index on each transaction FK
/// (docs/03-BUSINESS-RULES.md §13 "unique database constraint where practical").
/// </summary>
public class Review
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 2000;

    private Review()
    {
    }

    /// <summary>
    /// Records a review. Pass exactly one of <paramref name="orderId"/> or
    /// <paramref name="b2bDealId"/>.
    /// </summary>
    public Review(
        Guid reviewedMerchantProfileId,
        string reviewerUserId,
        Guid? orderId,
        Guid? b2bDealId,
        int rating,
        string? comment,
        DateTime nowUtc)
    {
        if ((orderId is null) == (b2bDealId is null))
        {
            throw new DomainException("A review must reference exactly one transaction — an order or a deal.");
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
        B2BDealId = b2bDealId;
        Rating = rating;
        Comment = NormalizeComment(comment);
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The merchant being reviewed — the selling merchant of the transaction.</summary>
    public Guid ReviewedMerchantProfileId { get; private set; }

    /// <summary>The Identity user id of the reviewer — the B2C buyer or the B2B buying merchant's user.</summary>
    public string ReviewerUserId { get; private set; } = null!;

    public Guid? OrderId { get; private set; }

    public Guid? B2BDealId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public TrustTransactionType TransactionType =>
        OrderId is not null ? TrustTransactionType.B2COrder : TrustTransactionType.B2BDeal;

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
