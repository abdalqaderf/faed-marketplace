using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;

namespace Faed.UnitTests;

/// <summary>
/// Listing aggregate rules (tasks/TASK-004-LISTINGS-AND-INVENTORY.md,
/// docs/03-BUSINESS-RULES.md §2-3-5, AGENTS.md Rules A-B, docs/17-DATA-INVARIANTS.md).
/// </summary>
public class ListingTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    // Neither is defect-related; tests that need a physical-imperfection combination pass
    // their own explicit code(s) instead (docs/03-BUSINESS-RULES.md §3).
    private const string DefaultGradeCode = "A";
    private static readonly string[] DefaultReasonCodes = ["Overstock"];

    private static Listing NewListing() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Men's Running Sneakers", "mens-running-sneakers",
        "Comfortable running sneakers.", Now);

    /// <summary>A listing with one option/value, one stocked variant and a product photo — the
    /// non-pricing prerequisites every submittable listing shares.</summary>
    private static Listing SubmittableListing(decimal? retailPrice = 24.5m)
    {
        var listing = NewListing();
        var option = listing.AddOption("Size", Now);
        var valueM = listing.AddOptionValue(option.Id, "M", Now);
        listing.AddVariant("SNK-M", [valueM.Id], initialQuantity: 5, Now);
        listing.AddMedia(ListingMediaType.Product, "key-1", "front.jpg", "image/jpeg", 1024, "Front view", Now);
        SetDetails(listing, retailPrice: retailPrice, discountReasonIds: [Guid.NewGuid()]);
        return listing;
    }

    private static void SetDetails(
        Listing listing,
        decimal? referencePrice = null,
        decimal? retailPrice = 24.5m,
        bool allowB2C = true,
        bool allowB2B = false,
        int? wholesaleMinQuantity = null,
        IReadOnlyCollection<Guid>? discountReasonIds = null,
        string title = "Men's Running Sneakers") =>
        listing.UpdateDetails(
            listing.CategoryId,
            listing.BrandId,
            listing.ConditionGradeId,
            title,
            listing.Description,
            referencePrice,
            retailPrice,
            wholesaleIndicativeUnitPrice: null,
            wholesaleMinQuantity,
            allowB2C,
            allowB2B,
            allowMixedVariantB2B: false,
            returnPolicyText: null,
            warrantyText: null,
            includedItemsText: null,
            missingItemsText: null,
            discountReasonIds ?? [Guid.NewGuid()],
            Now);

    [Fact]
    public void NewListing_StartsAsDraft_AndIsNotPubliclyVisible()
    {
        var listing = NewListing();

        Assert.Equal(ListingStatus.Draft, listing.Status);
        Assert.False(listing.IsPubliclyVisible);
    }

    [Fact]
    public void AddVariant_DuplicateOptionCombination_Throws()
    {
        var listing = NewListing();
        var option = listing.AddOption("Size", Now);
        var valueM = listing.AddOptionValue(option.Id, "M", Now);
        listing.AddVariant("SNK-M-1", [valueM.Id], 5, Now);

        Assert.Throws<DomainException>(() => listing.AddVariant("SNK-M-2", [valueM.Id], 3, Now));
    }

    [Fact]
    public void AddVariant_DistinctCombinations_BothSucceed()
    {
        // The mandatory example from TASK-004: Black/M, Black/L, White/M must all be
        // representable without a hard-coded clothing/shoe design.
        var listing = NewListing();
        var color = listing.AddOption("Colour", Now);
        var black = listing.AddOptionValue(color.Id, "Black", Now);
        var white = listing.AddOptionValue(color.Id, "White", Now);
        var size = listing.AddOption("Size", Now);
        var m = listing.AddOptionValue(size.Id, "M", Now);
        var l = listing.AddOptionValue(size.Id, "L", Now);

        listing.AddVariant("TSHIRT-BLK-M", [black.Id, m.Id], 4, Now);
        listing.AddVariant("TSHIRT-BLK-L", [black.Id, l.Id], 2, Now);
        listing.AddVariant("TSHIRT-WHT-M", [white.Id, m.Id], 3, Now);

        Assert.Equal(3, listing.Variants.Count);
        Assert.Equal(9, listing.AvailableUnits);
    }

    [Fact]
    public void AddVariant_MissingAnOptionValue_Throws()
    {
        var listing = NewListing();
        var size = listing.AddOption("Size", Now);
        listing.AddOptionValue(size.Id, "M", Now);
        listing.AddOption("Colour", Now);

        // Only Size is supplied; Colour is required too.
        Assert.Throws<DomainException>(() => listing.AddVariant("SKU-1", [], 1, Now));
    }

    [Fact]
    public void AddVariant_DuplicateSku_Throws()
    {
        var listing = NewListing();
        var size = listing.AddOption("Size", Now);
        var m = listing.AddOptionValue(size.Id, "M", Now);
        var l = listing.AddOptionValue(size.Id, "L", Now);
        listing.AddVariant("SKU-1", [m.Id], 1, Now);

        Assert.Throws<DomainException>(() => listing.AddVariant("SKU-1", [l.Id], 1, Now));
    }

    [Fact]
    public void SubmitForReview_WithoutProductPhoto_Throws()
    {
        var listing = NewListing();
        var size = listing.AddOption("Size", Now);
        var m = listing.AddOptionValue(size.Id, "M", Now);
        listing.AddVariant("SKU-1", [m.Id], 5, Now);
        SetDetails(listing);

        Assert.Throws<DomainException>(() => listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now));
        Assert.Contains(listing.DescribeSubmissionBlockers(DefaultGradeCode, DefaultReasonCodes), b => b.Contains("photo"));
    }

    [Fact]
    public void SubmitForReview_WhenB2CWithoutRetailPrice_Throws()
    {
        var listing = SubmittableListing(retailPrice: null);

        Assert.Throws<DomainException>(() => listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now));
    }

    [Fact]
    public void SubmitForReview_ReferencePriceWithoutEvidence_Throws()
    {
        var listing = SubmittableListing();
        SetDetails(listing, referencePrice: 49.9m, retailPrice: 24.5m, discountReasonIds: [Guid.NewGuid()]);

        Assert.Throws<DomainException>(() => listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now));
    }

    [Fact]
    public void SubmitForReview_ReferencePriceNotHigherThanRetail_Throws()
    {
        var listing = SubmittableListing();
        SetDetails(listing, referencePrice: 20m, retailPrice: 24.5m, discountReasonIds: [Guid.NewGuid()]);
        listing.AddReferencePriceEvidence(
            ReferencePriceEvidenceType.PreviousStorePrice, "https://example.com", null, null, null, null, Now);

        Assert.Throws<DomainException>(() => listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now));
    }

    [Fact]
    public void SubmitForReview_ValidListing_MovesToPendingReview_AndOpensModeration()
    {
        var listing = SubmittableListing();

        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
        Assert.NotNull(listing.PendingModeration);
        Assert.Equal("submitted for review", listing.PendingModeration!.ReasonForReview);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("D")]
    public void SubmitForReview_ConditionGradeClaimsAPhysicalImperfection_WithoutEvidence_Throws(string gradeCode)
    {
        // docs/03-BUSINESS-RULES.md §3: Grade B "packaging imperfection" and Grade D
        // "cosmetic imperfection" must be shown, not merely claimed.
        var listing = SubmittableListing();

        var ex = Assert.Throws<DomainException>(
            () => listing.SubmitForReview(gradeCode, DefaultReasonCodes, Now));
        Assert.Contains("defect or packaging photo", ex.Message);
        Assert.Contains(
            listing.DescribeSubmissionBlockers(gradeCode, DefaultReasonCodes),
            b => b.Contains("defect or packaging photo"));
    }

    [Theory]
    [InlineData("PackagingDamage")]
    [InlineData("CosmeticDefect")]
    public void SubmitForReview_DiscountReasonClaimsAPhysicalImperfection_WithoutEvidence_Throws(string reasonCode)
    {
        var listing = SubmittableListing();

        Assert.Throws<DomainException>(
            () => listing.SubmitForReview(DefaultGradeCode, [reasonCode], Now));
    }

    [Fact]
    public void SubmitForReview_GradeB_WithAPackagingPhoto_Succeeds()
    {
        var listing = SubmittableListing();
        listing.AddMedia(ListingMediaType.Packaging, "key-2", "box.jpg", "image/jpeg", 1024, null, Now);

        listing.SubmitForReview("B", DefaultReasonCodes, Now);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
    }

    [Fact]
    public void SubmitForReview_CosmeticDefectReason_WithADefectPhoto_Succeeds()
    {
        var listing = SubmittableListing();
        listing.AddMedia(ListingMediaType.Defect, "key-2", "scratch.jpg", "image/jpeg", 1024, "Cosmetic mark", Now);

        listing.SubmitForReview(DefaultGradeCode, ["CosmeticDefect"], Now);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
    }

    [Fact]
    public void Approve_WithStock_PublishesAsLive()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        listing.Approve("admin-1", "Looks good", Now);

        Assert.Equal(ListingStatus.Live, listing.Status);
        Assert.True(listing.IsPubliclyVisible);
        Assert.Equal(ListingModerationStatus.Approved, listing.LatestModeration!.Status);
    }

    [Fact]
    public void Approve_WithNoStock_PublishesAsSoldOut_NotLive()
    {
        var listing = SubmittableListing();
        var variant = listing.Variants.Single();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        listing.Approve("admin-1", null, Now);
        // Deplete the only variant via a manual correction, mirroring how the merchant would.
        variant.AdjustAvailable(-5, Now);
        listing.RefreshAvailability(Now);

        Assert.Equal(ListingStatus.SoldOut, listing.Status);
        Assert.False(listing.IsPubliclyVisible);
    }

    [Fact]
    public void Reject_RecordsReasonAndReturnsListingToRejected()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        listing.Reject("admin-1", "Missing defect disclosure", Now);

        Assert.Equal(ListingStatus.Rejected, listing.Status);
        Assert.Equal(ListingModerationStatus.Rejected, listing.LatestModeration!.Status);
        Assert.Equal("Missing defect disclosure", listing.LatestModeration.ReviewNote);
    }

    [Fact]
    public void MaterialEdit_OnLiveListing_ReturnsToPendingReview_WithoutLosingApprovalHistory()
    {
        // Regression test: UpdateDetails must apply every field — including discount reasons —
        // as one atomic transition. Calling a second material mutator after the aggregate has
        // already flipped Live -> PendingReview must not see itself locked out
        // (docs/02-SCOPE-AND-DECISIONS.md "Listing moderation policy").
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);
        Assert.Equal(ListingStatus.Live, listing.Status);

        SetDetails(listing, title: "Men's Running Sneakers (Updated)", discountReasonIds: [Guid.NewGuid()]);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
        Assert.NotNull(listing.PendingModeration);
        Assert.Contains("title", listing.PendingModeration!.ReasonForReview);
        // The prior approval is preserved, not overwritten.
        Assert.Contains(listing.Moderations, m => m.Status == ListingModerationStatus.Approved);
    }

    [Fact]
    public void NonMaterialEdit_OnLiveListing_StaysLive()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);

        listing.UpdateDetails(
            listing.CategoryId, listing.BrandId, listing.ConditionGradeId,
            listing.Title, listing.Description,
            listing.ReferencePrice, listing.RetailPrice, listing.WholesaleIndicativeUnitPrice,
            listing.WholesaleMinQuantity, listing.AllowB2C, listing.AllowB2B,
            allowMixedVariantB2B: true,
            returnPolicyText: "Returns accepted within 14 days",
            warrantyText: "No warranty",
            includedItemsText: listing.IncludedItemsText,
            missingItemsText: listing.MissingItemsText,
            listing.DiscountReasons.Select(r => r.DiscountReasonId).ToList(),
            Now);

        Assert.Equal(ListingStatus.Live, listing.Status);
    }

    [Fact]
    public void ConditionGrade_And_DiscountReasons_AreIndependentOnAListing()
    {
        // AGENTS.md Rule B: a past-season product may be physically perfect. Nothing on the
        // aggregate ties ConditionGradeId to a specific set of discount reasons.
        var gradeAId = Guid.NewGuid();
        var listing = new Listing(
            Guid.NewGuid(), Guid.NewGuid(), gradeAId,
            "Grade A Overstock Jacket", "grade-a-overstock-jacket", "Brand new, never worn.", Now);
        var pastSeasonReasonId = Guid.NewGuid();

        SetDetails(listing, discountReasonIds: [pastSeasonReasonId]);

        Assert.Equal(gradeAId, listing.ConditionGradeId);
        Assert.Contains(pastSeasonReasonId, listing.DiscountReasons.Select(r => r.DiscountReasonId));
    }

    [Fact]
    public void RemoveOption_WhileVariantsExist_Throws()
    {
        var listing = NewListing();
        var size = listing.AddOption("Size", Now);
        var m = listing.AddOptionValue(size.Id, "M", Now);
        listing.AddVariant("SKU-1", [m.Id], 1, Now);

        Assert.Throws<DomainException>(() => listing.RemoveOption(size.Id, Now));
    }

    [Fact]
    public void RemoveVariant_WithNoReservedOrSoldStock_Succeeds()
    {
        var listing = NewListing();
        var size = listing.AddOption("Size", Now);
        var m = listing.AddOptionValue(size.Id, "M", Now);
        var variant = listing.AddVariant("SKU-1", [m.Id], 5, Now);

        listing.RemoveVariant(variant.Id, Now);

        Assert.Empty(listing.Variants);
    }

    [Fact]
    public void Edit_WhilePendingReview_Throws()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.Throws<DomainException>(() => SetDetails(listing, title: "New title"));
    }

    [Fact]
    public void RemoveMedia_LastProductPhoto_Throws()
    {
        // A Live listing that drops to zero product photos would silently violate the
        // submission invariant "at least one product photo" — removing an ordinary photo
        // does not by itself re-run the submission checks, so the aggregate must refuse
        // outright rather than let the listing end up in that state.
        var listing = SubmittableListing();
        var onlyPhoto = listing.Media.Single(m => m.MediaType == ListingMediaType.Product);

        Assert.Throws<DomainException>(
            () => listing.RemoveMedia(onlyPhoto.Id, DefaultGradeCode, DefaultReasonCodes, Now));
        Assert.Single(listing.Media, m => m.MediaType == ListingMediaType.Product);
    }

    [Fact]
    public void RemoveMedia_ProductPhotoWithAReplacementAlreadyPresent_Succeeds()
    {
        var listing = SubmittableListing();
        var second = listing.AddMedia(
            ListingMediaType.Product, "key-2", "side.jpg", "image/jpeg", 1024, "Side view", Now);
        var first = listing.Media.Single(m => m.MediaType == ListingMediaType.Product && m.Id != second.Id);

        listing.RemoveMedia(first.Id, DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.Single(listing.Media, m => m.MediaType == ListingMediaType.Product);
    }

    [Fact]
    public void RemoveMedia_LastPackagingPhoto_Succeeds()
    {
        // Only Product photography carries the "at least one" rule; packaging photos are
        // optional, so removing the last one is not blocked — as long as the listing's grade
        // and reasons do not themselves disclose a physical imperfection.
        var listing = SubmittableListing();
        var packaging = listing.AddMedia(
            ListingMediaType.Packaging, "key-3", "box.jpg", "image/jpeg", 1024, null, Now);

        listing.RemoveMedia(packaging.Id, DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.DoesNotContain(listing.Media, m => m.MediaType == ListingMediaType.Packaging);
    }

    [Theory]
    [InlineData("B", "Overstock")]
    [InlineData("A", "PackagingDamage")]
    public void RemoveMedia_LastDisclosurePhoto_WhenAnImperfectionIsDisclosed_Throws(string gradeCode, string reasonCode)
    {
        // docs/03-BUSINESS-RULES.md §3: a listing whose grade or discount reason discloses a
        // physical imperfection must keep at least one defect/packaging photo. Removing an
        // ordinary packaging photo is not otherwise material and does not re-run the submission
        // checks, so a Live listing could otherwise be left publicly visible with no evidence.
        var listing = SubmittableListing();
        var packaging = listing.AddMedia(
            ListingMediaType.Packaging, "key-9", "box.jpg", "image/jpeg", 1024, null, Now);
        string[] reasonCodes = [reasonCode];

        Assert.Throws<DomainException>(() => listing.RemoveMedia(packaging.Id, gradeCode, reasonCodes, Now));
        Assert.Single(listing.Media, m => m.MediaType == ListingMediaType.Packaging);
    }

    [Fact]
    public void RemoveMedia_DisclosurePhoto_WhenAnotherRemains_Succeeds()
    {
        var listing = SubmittableListing();
        listing.AddMedia(ListingMediaType.Packaging, "key-a", "box-1.jpg", "image/jpeg", 1024, null, Now);
        var second = listing.AddMedia(ListingMediaType.Defect, "key-b", "scuff.jpg", "image/jpeg", 1024, null, Now);

        listing.RemoveMedia(second.Id, "B", DefaultReasonCodes, Now);

        Assert.Single(listing.Media, m => m.MediaType is ListingMediaType.Defect or ListingMediaType.Packaging);
    }

    [Fact]
    public void AddMedia_ProductPhoto_OnLiveListing_ReturnsToPendingReview_PreservingApprovalHistory()
    {
        // AGENTS.md §8: the Product gallery is what a buyer judges the item by, so adding one
        // to a published listing is a material change — the new photo must not be publicly
        // visible until an admin has reviewed it, exactly like a title or price edit.
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);
        Assert.Equal(ListingStatus.Live, listing.Status);

        listing.AddMedia(ListingMediaType.Product, "key-2", "side.jpg", "image/jpeg", 1024, "Side view", Now);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
        Assert.NotNull(listing.PendingModeration);
        Assert.Contains("product photo", listing.PendingModeration!.ReasonForReview);
        Assert.Contains(listing.Moderations, m => m.Status == ListingModerationStatus.Approved);
    }

    [Fact]
    public void RemoveMedia_ProductPhoto_OnLiveListing_WhenAnotherRemains_ReturnsToPendingReview()
    {
        var listing = SubmittableListing();
        var second = listing.AddMedia(
            ListingMediaType.Product, "key-2", "side.jpg", "image/jpeg", 1024, "Side view", Now);
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);

        listing.RemoveMedia(second.Id, DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.Equal(ListingStatus.PendingReview, listing.Status);
        Assert.Contains("product photo", listing.PendingModeration!.ReasonForReview);
    }

    [Fact]
    public void AddMedia_ProductPhoto_WhilePendingReview_Throws()
    {
        // A listing already awaiting a decision is frozen — a merchant cannot slip a new
        // Product photo past the reviewer mid-review.
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);

        Assert.Throws<DomainException>(() => listing.AddMedia(
            ListingMediaType.Product, "key-2", "side.jpg", "image/jpeg", 1024, null, Now));
    }

    [Fact]
    public void AddMedia_PackagingPhoto_OnLiveListing_StaysLive()
    {
        // Preserved TASK-005 behaviour: an ordinary packaging shot is not a material claim, so
        // it does not reopen moderation (only Product and Defect imagery do).
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);

        listing.AddMedia(ListingMediaType.Packaging, "key-2", "box.jpg", "image/jpeg", 1024, null, Now);

        Assert.Equal(ListingStatus.Live, listing.Status);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/price.pdf")]
    [InlineData("not a url")]
    public void AddReferencePriceEvidence_NonHttpUrl_Throws(string url)
    {
        // A reference-price link is later rendered as a clickable <a href> to admins and
        // buyers (docs/07-UI-UX-SPEC.md §9); an unchecked scheme would let a merchant plant a
        // javascript: or similarly hostile URL.
        var listing = NewListing();

        Assert.Throws<DomainException>(() => listing.AddReferencePriceEvidence(
            ReferencePriceEvidenceType.ProductUrl, url, null, null, null, null, Now));
    }

    [Fact]
    public void AddReferencePriceEvidence_ValidHttpsUrl_Succeeds()
    {
        var listing = NewListing();

        var evidence = listing.AddReferencePriceEvidence(
            ReferencePriceEvidenceType.ProductUrl, "https://example.com/product", null, null, null, null, Now);

        Assert.Equal("https://example.com/product", evidence.ReferenceUrl);
    }

    // Note: ListingModeration.AppendReason's multi-change accumulation branch (reached from
    // Listing.ApplyMaterialChange's PendingReview case) is not exercised here: every current
    // material mutator calls RequireMaterialEditAllowed() first, which always throws while
    // Status is PendingReview (see Edit_WhilePendingReview_Throws below) — so a listing can
    // never legitimately collect a second material change onto an already-open moderation
    // record today. AppendReason's exact-segment dedup fix (replacing a substring check that
    // could false-positive) is still correct defensively, for if a future task's workflow
    // ever makes that branch reachable.

    [Fact]
    public void HideByAdmin_MarksTheListing_SoTheMerchantCannotRestoreItThemselves()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);

        listing.HideByAdmin("admin-1", "Policy violation", Now);

        Assert.Equal(ListingStatus.Hidden, listing.Status);
        Assert.True(listing.HiddenByAdmin);
        Assert.Throws<DomainException>(() => listing.Restore(Now));
    }

    [Fact]
    public void Restore_AfterTheMerchantsOwnHide_Succeeds()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);
        listing.Hide(Now);

        listing.Restore(Now);

        Assert.Equal(ListingStatus.Live, listing.Status);
        Assert.False(listing.HiddenByAdmin);
    }

    [Fact]
    public void RestoreByAdmin_LiftsAnAdminTakedown_AndClearsTheFlag()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);
        listing.HideByAdmin("admin-1", "Policy violation", Now);

        listing.RestoreByAdmin("admin-2", Now);

        Assert.Equal(ListingStatus.Live, listing.Status);
        Assert.False(listing.HiddenByAdmin);
        // Now that the flag is cleared, the merchant is back in control of their own listing.
        listing.Hide(Now);
        listing.Restore(Now);
        Assert.Equal(ListingStatus.Live, listing.Status);
    }

    [Fact]
    public void RestoreByAdmin_WhenNotHidden_Throws()
    {
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);

        Assert.Throws<DomainException>(() => listing.RestoreByAdmin("admin-1", Now));
    }

    [Fact]
    public void RefreshAvailability_WithAnExplicitTotal_UsesThatTotal_NotTheLoadedVariantsCollection()
    {
        // Regression coverage for the concurrent-sibling-depletion fix: InventoryService now
        // supplies a freshly queried total rather than letting RefreshAvailability derive it
        // from Variants, which may not reflect a concurrent request's change to a sibling.
        var listing = SubmittableListing();
        listing.SubmitForReview(DefaultGradeCode, DefaultReasonCodes, Now);
        listing.Approve("admin-1", null, Now);
        Assert.Equal(ListingStatus.Live, listing.Status);

        // The loaded collection still shows stock (AvailableUnits > 0), but the caller passes
        // an externally-computed total of zero — the explicit total must win.
        listing.RefreshAvailability(currentAvailableUnits: 0, Now);
        Assert.Equal(ListingStatus.SoldOut, listing.Status);

        listing.RefreshAvailability(currentAvailableUnits: 3, Now);
        Assert.Equal(ListingStatus.Live, listing.Status);
    }

    [Fact]
    public void AddVariant_WithNoOptionsDefined_CreatesASingleUndifferentiatedSku()
    {
        // Backs the merchant workspace allowing "Add variant" with zero options: a merchant
        // selling one plain SKU should not be forced to invent an option.
        var listing = NewListing();

        var variant = listing.AddVariant("SKU-PLAIN", [], 5, Now);

        Assert.Empty(variant.OptionValues);
        Assert.Equal(5, listing.AvailableUnits);
    }
}

/// <summary>
/// The authoritative stock record (AGENTS.md Rule A, docs/adr/0002-INVENTORY-AT-VARIANT-LEVEL.md).
/// </summary>
public class ListingVariantTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ListingVariant NewVariant(int initialQuantity)
    {
        var listing = new Listing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Item", "test-item", "d", Now);
        var option = listing.AddOption("Size", Now);
        var value = listing.AddOptionValue(option.Id, "M", Now);
        return listing.AddVariant("SKU-1", [value.Id], initialQuantity, Now);
    }

    [Fact]
    public void AdjustAvailable_NegativeDeltaExceedingStock_Throws()
    {
        var variant = NewVariant(3);

        Assert.Throws<DomainException>(() => variant.AdjustAvailable(-4, Now));
        Assert.Equal(3, variant.AvailableQuantity);
    }

    [Fact]
    public void AdjustAvailable_PositiveDelta_IncreasesAvailableQuantity()
    {
        var variant = NewVariant(3);

        var after = variant.AdjustAvailable(5, Now);

        Assert.Equal(8, after);
        Assert.Equal(8, variant.AvailableQuantity);
    }

    [Fact]
    public void AdjustAvailable_ExactlyToZero_Succeeds()
    {
        var variant = NewVariant(3);

        var after = variant.AdjustAvailable(-3, Now);

        Assert.Equal(0, after);
        Assert.False(variant.IsSellable);
    }

    [Fact]
    public void AdjustAvailable_ZeroDelta_Throws()
    {
        var variant = NewVariant(3);

        Assert.Throws<DomainException>(() => variant.AdjustAvailable(0, Now));
    }

    [Fact]
    public void NewVariant_CannotStartNegative()
    {
        var listing = new Listing(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Item", "test-item", "d", Now);
        var option = listing.AddOption("Size", Now);
        var value = listing.AddOptionValue(option.Id, "M", Now);

        Assert.Throws<DomainException>(() => listing.AddVariant("SKU-1", [value.Id], -1, Now));
    }
}
