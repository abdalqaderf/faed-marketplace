using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Xunit;

namespace Faed.Web.Tests.Models.Entities;

/// <summary>
/// PHASE-PLAN.md Phase 1, tests 1 and 2: the listing lifecycle and its submission blockers.
/// Pure entity tests — no database, no services — since both are self-contained aggregate
/// behaviour. They exist for Phase 8, which rewrites <see cref="Listing.DescribeSubmissionBlockers"/>
/// into inline per-field validation: a regression there would only otherwise surface by hand.
/// </summary>
public class ListingTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private const string GradeCode = "A";
    private static readonly string[] ReasonCodes = ["Overstock"];

    // ---- Test 1: status transitions --------------------------------------------------

    [Fact]
    public void Lifecycle_DraftSubmittedThenApproved_MovesToLive()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: true);
        Assert.Equal(ListingStatus.Draft, listing.Status);

        listing.SubmitForReview(GradeCode, ReasonCodes, Now);
        Assert.Equal(ListingStatus.PendingReview, listing.Status);

        listing.Approve("admin-id", "Looks good.", Now);
        Assert.Equal(ListingStatus.Live, listing.Status);
    }

    [Fact]
    public void Approve_WithoutFirstSubmitting_Throws()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: true);

        Assert.Throws<DomainException>(() => listing.Approve("admin-id", "Looks good.", Now));
    }

    [Fact]
    public void SubmitForReview_WhenAlreadyLive_Throws()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: true);
        listing.SubmitForReview(GradeCode, ReasonCodes, Now);
        listing.Approve("admin-id", "Looks good.", Now);

        Assert.Throws<DomainException>(() => listing.SubmitForReview(GradeCode, ReasonCodes, Now));
    }

    [Fact]
    public void Hide_WhenStillDraft_Throws()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: true);

        Assert.Throws<DomainException>(() => listing.Hide(Now));
    }

    // ---- Test 2: submission blockers --------------------------------------------------

    [Fact]
    public void DescribeSubmissionBlockers_MissingProductPhoto_ReportsIt()
    {
        var listing = BuildListing(includePhoto: false, includeReason: true, includeVariant: true);

        var blockers = listing.DescribeSubmissionBlockers(GradeCode, ReasonCodes);

        Assert.Contains(blockers, b => b.Contains("product photo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DescribeSubmissionBlockers_MissingDiscountReason_ReportsIt()
    {
        var listing = BuildListing(includePhoto: true, includeReason: false, includeVariant: true);

        var blockers = listing.DescribeSubmissionBlockers(GradeCode, []);

        Assert.Contains(blockers, b => b.Contains("discounted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DescribeSubmissionBlockers_MissingActiveVariant_ReportsIt()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: false);

        var blockers = listing.DescribeSubmissionBlockers(GradeCode, ReasonCodes);

        Assert.Contains(blockers, b => b.Contains("active variant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DescribeSubmissionBlockers_WithEveryRequiredField_IsEmpty()
    {
        var listing = BuildListing(includePhoto: true, includeReason: true, includeVariant: true);

        var blockers = listing.DescribeSubmissionBlockers(GradeCode, ReasonCodes);

        Assert.Empty(blockers);
    }

    [Fact]
    public void SubmitForReview_WithABlocker_ThrowsReportingIt()
    {
        var listing = BuildListing(includePhoto: false, includeReason: true, includeVariant: true);

        var ex = Assert.Throws<DomainException>(() => listing.SubmitForReview(GradeCode, ReasonCodes, Now));

        Assert.Contains("product photo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Draft listing with a retail price and, selectively, the three fields
    /// <c>DescribeSubmissionBlockers</c> checks beyond price: a product photo, a discount
    /// reason and an active variant.
    /// </summary>
    private static Listing BuildListing(bool includePhoto, bool includeReason, bool includeVariant)
    {
        var listing = new Listing(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Kettle", "test-kettle", "A test description.", Now);

        if (includeVariant)
        {
            listing.AddVariant("SKU-KETTLE", [], initialQuantity: 1, Now);
        }

        if (includePhoto)
        {
            listing.AddMedia(ListingMediaType.Product, "key-kettle", "photo.jpg", "image/jpeg", 100, null, Now);
        }

        listing.UpdateDetails(
            listing.CategoryId, listing.ConditionGradeId, listing.Title, listing.Description,
            referencePrice: null, retailPrice: 25m, returnPolicyText: null, warrantyType: WarrantyType.None,
            warrantyMonths: null, includedItemsText: null, missingItemsText: null,
            discountReasonIds: includeReason ? [Guid.NewGuid()] : [], Now);

        return listing;
    }
}
