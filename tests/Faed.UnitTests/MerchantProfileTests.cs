using Faed.Domain.Entities;
using Faed.Domain.Enums;
using Faed.Domain.Exceptions;

namespace Faed.UnitTests;

/// <summary>
/// Merchant verification state machine (docs/03-BUSINESS-RULES.md §1,
/// docs/05-USER-FLOWS-AND-STATE-MACHINES.md §1). A user can never self-assign Approved,
/// and every decision is a guarded transition.
/// </summary>
public class MerchantProfileTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private static MerchantProfile NewProfile() => new("user-1", "Amman Threads", "amman-threads", Now);

    private static MerchantProfile WithDocument(MerchantProfile profile)
    {
        profile.AddDocument(MerchantVerificationDocumentType.CommercialRegistration, "key", "reg.pdf", "application/pdf", 10, Now);
        return profile;
    }

    [Fact]
    public void NewProfile_StartsAsDraft_AndCannotSell()
    {
        var profile = NewProfile();

        Assert.Equal(MerchantVerificationStatus.Draft, profile.VerificationStatus);
        Assert.False(profile.CanSell);
        Assert.True(profile.IsEditable);
    }

    [Fact]
    public void SubmitForReview_WithoutDocuments_Throws()
    {
        var profile = NewProfile();

        Assert.Throws<DomainException>(() => profile.SubmitForReview(Now));
    }

    [Fact]
    public void SubmitForReview_WithDocument_MovesToPendingReview()
    {
        var profile = WithDocument(NewProfile());

        profile.SubmitForReview(Now);

        Assert.Equal(MerchantVerificationStatus.PendingReview, profile.VerificationStatus);
        Assert.Equal(Now, profile.SubmittedAtUtc);
        Assert.False(profile.IsEditable);
    }

    [Fact]
    public void AddDocument_WhilePendingReview_Throws()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);

        Assert.Throws<DomainException>(() =>
            profile.AddDocument(MerchantVerificationDocumentType.Other, "k", "f.pdf", "application/pdf", 1, Now));
    }

    [Fact]
    public void Approve_FromPendingReview_RecordsReviewer()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);

        profile.Approve("admin-9", Now.AddHours(1));

        Assert.Equal(MerchantVerificationStatus.Approved, profile.VerificationStatus);
        Assert.True(profile.CanSell);
        Assert.Equal("admin-9", profile.ReviewedByAdminId);
        Assert.Equal(Now.AddHours(1), profile.ReviewedAtUtc);
    }

    [Fact]
    public void Approve_FromDraft_Throws()
    {
        var profile = NewProfile();

        Assert.Throws<DomainException>(() => profile.Approve("admin-9", Now));
    }

    [Fact]
    public void Reject_RequiresReason_AndReturnsToEditable()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);

        Assert.Throws<DomainException>(() => profile.Reject("admin-9", "   ", Now));

        profile.Reject("admin-9", "Document unreadable", Now);

        Assert.Equal(MerchantVerificationStatus.Rejected, profile.VerificationStatus);
        Assert.Equal("Document unreadable", profile.RejectionReason);
        Assert.True(profile.IsEditable);
    }

    [Fact]
    public void Reject_WithOverlongReason_Throws()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);

        var tooLong = new string('x', MerchantProfile.MaxDecisionReasonLength + 1);

        Assert.Throws<DomainException>(() => profile.Reject("admin-9", tooLong, Now));
    }

    [Fact]
    public void Resubmit_AfterRejection_ClearsReasonAndPending()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);
        profile.Reject("admin-9", "Fix documents", Now);

        profile.SubmitForReview(Now.AddDays(1));

        Assert.Equal(MerchantVerificationStatus.PendingReview, profile.VerificationStatus);
        Assert.Null(profile.RejectionReason);
    }

    [Fact]
    public void Suspend_OnlyFromApproved_AndReinstateRestores()
    {
        var profile = WithDocument(NewProfile());
        profile.SubmitForReview(Now);

        Assert.Throws<DomainException>(() => profile.Suspend("admin-9", "abuse", Now));

        profile.Approve("admin-9", Now);
        profile.Suspend("admin-9", "Policy breach", Now);
        Assert.Equal(MerchantVerificationStatus.Suspended, profile.VerificationStatus);
        Assert.False(profile.CanSell);

        profile.Reinstate("admin-9", Now.AddDays(2));
        Assert.Equal(MerchantVerificationStatus.Approved, profile.VerificationStatus);
        Assert.True(profile.CanSell);
    }

    [Fact]
    public void RemoveDocument_DeactivatesButKeepsHistory()
    {
        var profile = NewProfile();
        var doc = profile.AddDocument(MerchantVerificationDocumentType.CommercialRegistration, "key", "reg.pdf", "application/pdf", 10, Now);

        profile.RemoveDocument(doc.Id, Now);

        Assert.Empty(profile.ActiveDocuments);
        Assert.Single(profile.Documents);
    }
}
