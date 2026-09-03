using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// Review aggregate rules (tasks/TASK-009-TRUST.md, docs/03-BUSINESS-RULES.md §13,
/// docs/17-DATA-INVARIANTS.md "Review").
/// </summary>
public class ReviewTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_review_captures_the_rating_and_transaction()
    {
        var review = new Review(Guid.NewGuid(), "buyer-1", Guid.NewGuid(), null, 4, "Solid, as described.", Now);

        Assert.Equal(4, review.Rating);
        Assert.Equal(TrustTransactionType.B2COrder, review.TransactionType);
        Assert.Equal("Solid, as described.", review.Comment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rating_outside_one_to_five_is_rejected(int rating)
    {
        Assert.Throws<DomainException>(() => new Review(
            Guid.NewGuid(), "buyer-1", Guid.NewGuid(), null, rating, null, Now));
    }

    [Fact]
    public void Review_with_both_transaction_references_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Review(
            Guid.NewGuid(), "buyer-1", Guid.NewGuid(), Guid.NewGuid(), 5, null, Now));
    }

    [Fact]
    public void Review_with_no_transaction_reference_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Review(
            Guid.NewGuid(), "buyer-1", null, null, 5, null, Now));
    }

    [Fact]
    public void Blank_comment_is_stored_as_null()
    {
        var review = new Review(Guid.NewGuid(), "buyer-1", null, Guid.NewGuid(), 5, "   ", Now);

        Assert.Null(review.Comment);
        Assert.Equal(TrustTransactionType.B2BDeal, review.TransactionType);
    }

    [Fact]
    public void Over_long_comment_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Review(
            Guid.NewGuid(), "buyer-1", Guid.NewGuid(), null, 5, new string('x', Review.MaxCommentLength + 1), Now));
    }
}
