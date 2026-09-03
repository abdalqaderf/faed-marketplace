using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// Dispute aggregate rules (tasks/TASK-009-TRUST.md, docs/03-BUSINESS-RULES.md §14,
/// docs/05-USER-FLOWS-AND-STATE-MACHINES.md §10, docs/17-DATA-INVARIANTS.md "Dispute").
/// </summary>
public class DisputeTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Guid OrderId = Guid.NewGuid();

    private static Dispute NewOrderDispute() => new(
        orderId: OrderId,
        b2bDealId: null,
        raisedByUserId: "buyer-1",
        reasonCode: DisputeReasonCode.UndisclosedDefect,
        description: "The jacket had a torn lining that was not shown.",
        nowUtc: Now);

    /// <summary>A dispute already picked up for review — the only state from which it can close.</summary>
    private static Dispute UnderReviewDispute()
    {
        var dispute = NewOrderDispute();
        dispute.StartReview("admin-1", Now.AddHours(1));
        return dispute;
    }

    [Fact]
    public void New_dispute_starts_open_and_references_exactly_one_transaction()
    {
        var dispute = NewOrderDispute();

        Assert.Equal(DisputeStatus.Open, dispute.Status);
        Assert.Equal(TrustTransactionType.B2COrder, dispute.TransactionType);
        Assert.NotNull(dispute.OrderId);
        Assert.Null(dispute.B2BDealId);
    }

    [Fact]
    public void New_dispute_holds_the_active_transaction_key()
    {
        var dispute = NewOrderDispute();

        Assert.Equal(Dispute.ActiveKeyFor(TrustTransactionType.B2COrder, OrderId), dispute.ActiveTransactionKey);
    }

    [Fact]
    public void New_dispute_with_both_transaction_references_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Dispute(
            Guid.NewGuid(), Guid.NewGuid(), "u", DisputeReasonCode.Other, "x", Now));
    }

    [Fact]
    public void New_dispute_with_no_transaction_reference_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Dispute(
            null, null, "u", DisputeReasonCode.Other, "x", Now));
    }

    [Fact]
    public void New_dispute_with_blank_description_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Dispute(
            Guid.NewGuid(), null, "u", DisputeReasonCode.Other, "   ", Now));
    }

    [Fact]
    public void StartReview_moves_open_dispute_to_under_review()
    {
        var dispute = NewOrderDispute();

        dispute.StartReview("admin-1", Now.AddHours(1));

        Assert.Equal(DisputeStatus.UnderReview, dispute.Status);
        Assert.Equal("admin-1", dispute.ResolvedByAdminId);
        // Still an active dispute — the key is only released when it closes.
        Assert.NotNull(dispute.ActiveTransactionKey);
    }

    [Fact]
    public void StartReview_on_a_closed_dispute_is_rejected()
    {
        var dispute = UnderReviewDispute();
        dispute.Resolve("admin-1", "Refund issued.", Now.AddHours(2));

        Assert.Throws<DomainException>(() => dispute.StartReview("admin-2", Now.AddHours(3)));
    }

    [Fact]
    public void An_open_dispute_cannot_be_resolved_or_rejected_directly()
    {
        Assert.Throws<DomainException>(() => NewOrderDispute().Resolve("admin-1", "Upheld.", Now.AddHours(1)));
        Assert.Throws<DomainException>(() => NewOrderDispute().Reject("admin-1", "Dismissed.", Now.AddHours(1)));
    }

    [Fact]
    public void Resolve_from_under_review_records_the_outcome_clears_the_key_and_is_terminal()
    {
        var dispute = UnderReviewDispute();

        dispute.Resolve("admin-1", "Merchant agreed to a partial refund.", Now.AddHours(2));

        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        Assert.Equal("Merchant agreed to a partial refund.", dispute.AdminResolution);
        Assert.Equal(Now.AddHours(2), dispute.ResolvedAtUtc);
        Assert.True(dispute.IsTerminal);
        Assert.Null(dispute.ActiveTransactionKey);
    }

    [Fact]
    public void Reject_from_under_review_records_the_reason_and_clears_the_key()
    {
        var dispute = UnderReviewDispute();

        dispute.Reject("admin-1", "The defect was disclosed on the listing.", Now.AddHours(2));

        Assert.Equal(DisputeStatus.Rejected, dispute.Status);
        Assert.Equal("The defect was disclosed on the listing.", dispute.AdminResolution);
        Assert.Null(dispute.ActiveTransactionKey);
    }

    [Fact]
    public void Resolve_without_an_outcome_is_rejected()
    {
        var dispute = UnderReviewDispute();

        Assert.Throws<DomainException>(() => dispute.Resolve("admin-1", "  ", Now.AddHours(2)));
    }

    [Fact]
    public void A_full_length_resolution_is_accepted_verbatim()
    {
        var dispute = UnderReviewDispute();
        var outcome = new string('x', Dispute.MaxResolutionLength);

        dispute.Resolve("admin-1", outcome, Now.AddHours(2));

        Assert.Equal(outcome, dispute.AdminResolution);
    }

    [Fact]
    public void Closing_an_already_closed_dispute_is_rejected()
    {
        var dispute = UnderReviewDispute();
        dispute.Resolve("admin-1", "done", Now.AddHours(2));

        Assert.Throws<DomainException>(() => dispute.Reject("admin-2", "changed my mind", Now.AddHours(3)));
    }

    [Fact]
    public void Evidence_can_be_added_while_open_but_not_after_it_is_closed()
    {
        var dispute = NewOrderDispute();

        dispute.AddEvidence("buyer-1", "key-1", "photo.jpg", "image/jpeg", 1024, Now.AddMinutes(5));
        Assert.Single(dispute.Evidence);

        dispute.StartReview("admin-1", Now.AddHours(1));
        dispute.Resolve("admin-1", "done", Now.AddHours(2));
        Assert.Throws<DomainException>(() =>
            dispute.AddEvidence("buyer-1", "key-2", "more.jpg", "image/jpeg", 1024, Now.AddHours(3)));
    }
}
