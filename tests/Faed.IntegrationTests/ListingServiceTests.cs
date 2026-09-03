using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Listing, variant and moderation use cases against real SQL Server
/// (tasks/TASK-004-LISTINGS-AND-INVENTORY.md exit criteria, docs/09-TEST-STRATEGY.md §3
/// "Listing" and "Inventory"). EF Core InMemory/SQLite are deliberately not used
/// (docs/09-TEST-STRATEGY.md §2).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class ListingServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task NonLiveListing_ImageIsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin_AndPublicOnceLive()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();

        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var mediaId = await scope.Db.ListingMedia
            .Where(m => m.ListingId == listingId).Select(m => m.Id).SingleAsync();

        var anonymousBeforeApproval = await scope.Media.OpenImageAsync(null, mediaId);
        Assert.True(anonymousBeforeApproval.Failed);

        var ownerBeforeApproval = await scope.Media.OpenImageAsync(userId, mediaId);
        Assert.True(ownerBeforeApproval.Succeeded);

        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var adminBeforeApproval = await scope.Media.OpenImageAsync(adminId, mediaId);
        Assert.True(adminBeforeApproval.Succeeded);

        var submit = await scope.Listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(submit.Succeeded, submit.Error);
        var approve = await scope.Moderation.ApproveAsync(adminId, listingId, null);
        Assert.True(approve.Succeeded, approve.Error);

        var anonymousAfterApproval = await scope.Media.OpenImageAsync(null, mediaId);
        Assert.True(anonymousAfterApproval.Succeeded);
    }

    [SkippableFact]
    public async Task MaterialEdit_OnALiveListing_ReturnsItToPendingReview_AndPreservesTheApprovalHistory()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, "Looks good")).Succeeded);

        var beforeEdit = await scope.Db.Listings.AsNoTracking().SingleAsync(l => l.Id == listingId);
        Assert.Equal(ListingStatus.Live, beforeEdit.Status);

        var current = await scope.Listings.GetMyListingAsync(userId, listingId);
        Assert.NotNull(current);
        var updated = current! with { Title = "Men's Running Sneakers (Updated)" };
        var edit = await scope.Listings.UpdateDetailsAsync(userId, listingId, ToInput(updated));
        Assert.True(edit.Succeeded, edit.Error);

        var afterEdit = await scope.Db.Listings.AsNoTracking().SingleAsync(l => l.Id == listingId);
        Assert.Equal(ListingStatus.PendingReview, afterEdit.Status);
        Assert.Equal("Men's Running Sneakers (Updated)", afterEdit.Title);

        var moderations = await scope.Db.ListingModerations.AsNoTracking()
            .Where(m => m.ListingId == listingId).ToListAsync();
        Assert.Contains(moderations, m => m.Status == ListingModerationStatus.Approved);
        Assert.Contains(moderations, m => m.Status == ListingModerationStatus.Pending);
    }

    [SkippableFact]
    public async Task DuplicateOptionCombination_IsRejectedByTheDatabase_EvenAcrossTwoConcurrentContexts()
    {
        // The aggregate already refuses an in-memory duplicate (see Faed.UnitTests); this
        // proves the unique index is the real backstop for two requests racing each other
        // (docs/17-DATA-INVARIANTS.md "One Listing cannot have duplicate option-value combinations").
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId);

        // CreateSubmittableListingAsync already added a variant for the "M" value; add a
        // second, still-unused value so the race is against each other, not against setup data.
        var optionId = await scope.Db.Set<ListingOption>()
            .Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
        var addValue = await scope.Listings.AddOptionValueAsync(userId, listingId, optionId, "L");
        Assert.True(addValue.Succeeded, addValue.Error);
        var optionValueId = await scope.Db.Set<ListingOptionValue>()
            .Where(v => v.ListingOptionId == optionId && v.Value == "L").Select(v => v.Id).SingleAsync();

        await using var ctx1 = scope.CreateDbContext();
        await using var ctx2 = scope.CreateDbContext();

        var listing1 = await ctx1.Listings
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants)
            .SingleAsync(l => l.Id == listingId);
        var listing2 = await ctx2.Listings
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants)
            .SingleAsync(l => l.Id == listingId);

        listing1.AddVariant("RACE-1", [optionValueId], 1, DateTime.UtcNow);
        listing2.AddVariant("RACE-2", [optionValueId], 1, DateTime.UtcNow);

        await ctx1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task AdjustStock_CannotGoNegative_AndIsAudited()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var variantId = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();

        var overdraw = await scope.Inventory.AdjustStockAsync(
            userId, new StockAdjustmentInput(variantId, InventoryAdjustmentType.StockLostOrDamaged, -999, "too many"));
        Assert.True(overdraw.Failed);

        var stillFive = await scope.Db.ListingVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.AvailableQuantity).SingleAsync();
        Assert.Equal(5, stillFive);

        var valid = await scope.Inventory.AdjustStockAsync(
            userId, new StockAdjustmentInput(variantId, InventoryAdjustmentType.StockLostOrDamaged, -2, "damaged in transit"));
        Assert.True(valid.Succeeded, valid.Error);
        Assert.Equal(3, valid.Value);

        var audit = await scope.Db.InventoryAdjustments.AsNoTracking()
            .SingleAsync(a => a.ListingVariantId == variantId);
        Assert.Equal(userId, audit.ChangedByUserId);
        Assert.Equal(-2, audit.QuantityDelta);
        Assert.Equal(5, audit.QuantityBefore);
        Assert.Equal(3, audit.QuantityAfter);
        Assert.Equal("damaged in transit", audit.Reason);
    }

    [SkippableFact]
    public async Task AdjustStock_TwoConcurrentContexts_OnlyTheFirstSaveSucceeds()
    {
        // Proves the rowversion protection AGENTS.md §7 requires for every quantity-bearing
        // variant. The literal "two buyers race for the last unit" scenario belongs to the
        // B2C order flow (a later task); this is the TASK-004-scoped proof that the
        // concurrency token itself stops a lost update on the variant it protects.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId, initialQuantity: 1);
        var variantId = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();

        await using var ctx1 = scope.CreateDbContext();
        await using var ctx2 = scope.CreateDbContext();

        var variant1 = await ctx1.ListingVariants.SingleAsync(v => v.Id == variantId);
        var variant2 = await ctx2.ListingVariants.SingleAsync(v => v.Id == variantId);

        variant1.AdjustAvailable(-1, DateTime.UtcNow);
        variant2.AdjustAvailable(-1, DateTime.UtcNow);

        await ctx1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());

        var finalQuantity = await scope.Db.ListingVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.AvailableQuantity).SingleAsync();
        Assert.Equal(0, finalQuantity);
    }

    [SkippableFact]
    public async Task ReferencePriceEvidenceFile_IsRetrievableByOwnerAndAdmin_ButNotAnonymous()
    {
        // AGENTS.md §8 "the reviewing admin sees them all" requires the uploaded file itself
        // to be retrievable, not only its metadata.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);

        var addEvidence = await scope.Listings.AddReferencePriceEvidenceAsync(userId, listingId,
            new AddReferencePriceEvidenceInput(
                ReferencePriceEvidenceType.InvoiceOrCatalogDocument,
                ReferenceUrl: null,
                Note: null,
                TestImages.MinimalPngStream(),
                "invoice.png",
                "image/png",
                TestImages.MinimalPng.Length));
        Assert.True(addEvidence.Succeeded, addEvidence.Error);

        var evidenceId = await scope.Db.ListingReferencePriceEvidence
            .Where(e => e.ListingId == listingId).Select(e => e.Id).SingleAsync();

        var anonymous = await scope.Media.OpenReferencePriceEvidenceAsync(null, evidenceId);
        Assert.True(anonymous.Failed);

        var owner = await scope.Media.OpenReferencePriceEvidenceAsync(userId, evidenceId);
        Assert.True(owner.Succeeded, owner.Error);

        var admin = await scope.Media.OpenReferencePriceEvidenceAsync(adminId, evidenceId);
        Assert.True(admin.Succeeded, admin.Error);

        // Never public even once the listing is Live and its product photos are.
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);
        var stillAnonymous = await scope.Media.OpenReferencePriceEvidenceAsync(null, evidenceId);
        Assert.True(stillAnonymous.Failed);
    }

    [SkippableFact]
    public async Task Approve_WhenTheMerchantIsNoLongerApproved_Fails_AndListingStaysPending()
    {
        // docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved" — a merchant
        // suspended between submission and the admin's decision must not still get published.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, merchantId) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);

        var profile = await scope.Db.MerchantProfiles.SingleAsync(p => p.Id == merchantId);
        profile.Suspend(adminId, "Compliance hold", DateTime.UtcNow);
        await scope.Db.SaveChangesAsync();

        var approve = await scope.Moderation.ApproveAsync(adminId, listingId, null);

        Assert.True(approve.Failed);
        var status = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.PendingReview, status);
    }

    [SkippableFact]
    public async Task RemoveImage_TheLastDisclosurePhotoOfAGradeBListing_IsRejected_AndTheListingStaysLive()
    {
        // docs/03-BUSINESS-RULES.md §3: a Grade B listing must show its packaging imperfection.
        // Removing an ordinary packaging photo is not otherwise material and does not re-run the
        // submission checks, so without this guard a Live listing could be left publicly visible
        // with no visual evidence at all.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId, conditionGradeCode: "B");

        var addPackaging = await scope.Listings.AddImageAsync(userId, listingId, new AddListingImageInput(
            ListingMediaType.Packaging, TestImages.MinimalPngStream(), "box.png", "image/png",
            TestImages.MinimalPng.Length, "Torn box corner"));
        Assert.True(addPackaging.Succeeded, addPackaging.Error);

        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var packagingId = await scope.Db.ListingMedia
            .Where(m => m.ListingId == listingId && m.MediaType == ListingMediaType.Packaging)
            .Select(m => m.Id).SingleAsync();

        var remove = await scope.Listings.RemoveImageAsync(userId, listingId, packagingId);

        Assert.True(remove.Failed);
        Assert.True(await scope.Db.ListingMedia.AnyAsync(m => m.Id == packagingId));
        var status = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.Live, status);
    }

    [SkippableFact]
    public async Task SoldOutListingImage_IsHiddenFromAnonymous_ButVisibleToOwnerAndAdmin()
    {
        // docs/03-BUSINESS-RULES.md §2: a sold-out listing is "addressable to authorized
        // users" — not to anonymous public traffic. Only Live is public.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId, initialQuantity: 0);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var status = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.SoldOut, status);

        var mediaId = await scope.Db.ListingMedia
            .Where(m => m.ListingId == listingId).Select(m => m.Id).SingleAsync();

        var anonymous = await scope.Media.OpenImageAsync(null, mediaId);
        Assert.True(anonymous.Failed);

        var owner = await scope.Media.OpenImageAsync(userId, mediaId);
        Assert.True(owner.Succeeded, owner.Error);

        var admin = await scope.Media.OpenImageAsync(adminId, mediaId);
        Assert.True(admin.Succeeded, admin.Error);
    }

    [SkippableFact]
    public async Task Create_WithTheNonLeafCategoryRoot_IsRejected()
    {
        // docs/04-DOMAIN-MODEL.md §2: a listing attaches to a leaf category, never the sector
        // root itself ("Fashion Overstock" is not a shoppable category).
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var rootCategoryId = await scope.Db.Categories
            .Where(c => c.ParentCategoryId == null).Select(c => c.Id).SingleAsync();
        var conditionGradeId = (await scope.Listings.GetReferenceDataAsync()).ConditionGrades[0].Id;

        var create = await scope.Listings.CreateAsync(userId, new ListingDetailsInput(
            rootCategoryId, null, conditionGradeId,
            "Root Category Attempt", "Should be rejected.",
            null, 10m, null, null,
            AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
            null, null, null, null, []));

        Assert.True(create.Failed);
    }

    [SkippableFact]
    public async Task AdjustStock_OnAnArchivedListing_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var variantId = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();

        Assert.True((await scope.Listings.ArchiveAsync(userId, listingId)).Succeeded);

        var adjust = await scope.Inventory.AdjustStockAsync(
            userId, new StockAdjustmentInput(variantId, InventoryAdjustmentType.ManualCorrection, 1, "should not apply"));

        Assert.True(adjust.Failed);
        var quantity = await scope.Db.ListingVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.AvailableQuantity).SingleAsync();
        Assert.Equal(5, quantity);
    }

    [SkippableFact]
    public async Task MerchantRestore_AfterAnAdminTakedown_Fails_AndOnlyAdminRestoreWorks()
    {
        // The merchant must not be able to reverse an admin's takedown of their own listing
        // (docs/16-PERMISSIONS-MATRIX.md "Moderate listing — Admin only").
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        Assert.True((await scope.Moderation.HideAsync(adminId, listingId, "Policy violation")).Succeeded);

        var merchantRestore = await scope.Listings.RestoreAsync(userId, listingId);
        Assert.True(merchantRestore.Failed);
        var stillHidden = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.Hidden, stillHidden);

        var adminRestore = await scope.Moderation.RestoreAsync(adminId, listingId);
        Assert.True(adminRestore.Succeeded, adminRestore.Error);
        var nowLive = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.Live, nowLive);
    }

    [SkippableFact]
    public async Task AdjustStock_DepletingTwoVariantsAcrossSeparateRequests_ListingBecomesSoldOut()
    {
        // Two separate, independently-scoped requests, each emptying a different variant on
        // the same listing, must still leave the listing SoldOut once both have applied — the
        // second request's fresh sibling query (rather than a stale navigation collection) is
        // what makes it see the first request's already-committed depletion. This does not
        // reproduce genuinely simultaneous commits (see Listing.RefreshAvailability's XML doc
        // and Faed.UnitTests.ListingTests.RefreshAvailability_WithAnExplicitTotal_* for the
        // deterministic unit-level proof of the underlying mechanism); a fully rigorous test of
        // true simultaneity would need an interleaving seam this fix doesn't add.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new ListingScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId, initialQuantity: 1);

        var optionId = await scope.Db.Set<ListingOption>()
            .Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
        var addValue = await scope.Listings.AddOptionValueAsync(userId, listingId, optionId, "L");
        Assert.True(addValue.Succeeded, addValue.Error);
        var lValueId = await scope.Db.Set<ListingOptionValue>()
            .Where(v => v.ListingOptionId == optionId && v.Value == "L").Select(v => v.Id).SingleAsync();
        var addSecondVariant = await scope.Listings.AddVariantAsync(
            userId, listingId, new AddVariantInput("SNK-L", [lValueId], 1));
        Assert.True(addSecondVariant.Succeeded, addSecondVariant.Error);

        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var variantIds = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).ToListAsync();
        Assert.Equal(2, variantIds.Count);

        await using var scope1 = new ListingScope(factory);
        var adjust1 = await scope1.Inventory.AdjustStockAsync(userId, new StockAdjustmentInput(
            variantIds[0], InventoryAdjustmentType.StockLostOrDamaged, -1, "damaged"));
        Assert.True(adjust1.Succeeded, adjust1.Error);

        await using var scope2 = new ListingScope(factory);
        var adjust2 = await scope2.Inventory.AdjustStockAsync(userId, new StockAdjustmentInput(
            variantIds[1], InventoryAdjustmentType.StockLostOrDamaged, -1, "damaged"));
        Assert.True(adjust2.Succeeded, adjust2.Error);

        var finalStatus = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.SoldOut, finalStatus);
    }

    private static ListingDetailsInput ToInput(ListingDetailView listing) => new(
        listing.CategoryId,
        listing.BrandId,
        listing.ConditionGradeId,
        listing.Title,
        listing.Description,
        listing.ReferencePrice,
        listing.RetailPrice,
        listing.WholesaleIndicativeUnitPrice,
        listing.WholesaleMinQuantity,
        listing.AllowB2C,
        listing.AllowB2B,
        listing.AllowMixedVariantB2B,
        listing.ReturnPolicyText,
        listing.WarrantyText,
        listing.IncludedItemsText,
        listing.MissingItemsText,
        listing.DiscountReasonIds);

    /// <summary>
    /// Creates real merchant/listing rows in the shared <c>Faed_WebTests</c> database
    /// (docs/09-TEST-STRATEGY.md §2 — one hosted factory per test collection) and removes
    /// them on dispose. Cleanup matters here specifically because <c>Listing.CategoryId</c>
    /// has a restricted delete behaviour: a category a test listing still references cannot
    /// be deleted by an unrelated catalog test running later in the same collection
    /// (docs/04-DOMAIN-MODEL.md §12).
    /// </summary>
    private sealed class ListingScope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];

        public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

        public IInventoryService Inventory => _scope.ServiceProvider.GetRequiredService<IInventoryService>();

        public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

        public IListingMediaService Media => _scope.ServiceProvider.GetRequiredService<IListingMediaService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        public ApplicationDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(Db.Database.GetConnectionString()
                    ?? throw new InvalidOperationException("The test DbContext has no connection string."))
                .Options);

        public async Task<string> CreateUserAsync(string? role = null)
        {
            var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = $"{Guid.NewGuid():N}@test.local",
                Email = $"{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
            };
            var created = await users.CreateAsync(user);
            Assert.True(created.Succeeded);

            if (role is not null)
            {
                var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }

                await users.AddToRoleAsync(user, role);
            }

            return user.Id;
        }

        /// <summary>Creates a user with an Approved merchant profile, bypassing the document
        /// upload/admin-review flow already covered by <c>MerchantVerificationServiceTests</c>.</summary>
        public async Task<(string UserId, Guid MerchantProfileId)> CreateApprovedMerchantAsync()
        {
            var userId = await CreateUserAsync();
            var now = DateTime.UtcNow;
            var profile = new MerchantProfile(userId, $"Test Merchant {Guid.NewGuid():N}", $"test-{Guid.NewGuid():N}", now);
            profile.AddDocument(MerchantVerificationDocumentType.CommercialRegistration, "test-key", "reg.pdf", "application/pdf", 10, now);
            profile.SubmitForReview(now);
            profile.Approve("test-admin-seed", now);

            Db.MerchantProfiles.Add(profile);
            await Db.SaveChangesAsync();
            _merchantProfileIds.Add(profile.Id);
            return (userId, profile.Id);
        }

        /// <summary>A listing with one option/value, one stocked variant, a product photo, a
        /// discount reason and a retail price — ready to submit for review.</summary>
        public async Task<Guid> CreateSubmittableListingAsync(
            string userId, int initialQuantity = 5, string? conditionGradeCode = null)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var conditionGradeId = conditionGradeCode is null
                ? referenceData.ConditionGrades[0].Id
                : referenceData.ConditionGrades.Single(g => g.Label.Contains($"Grade {conditionGradeCode} ")).Id;
            // Not "[0]" (alphabetically "Cosmetic Defect") — this listing carries no defect
            // photo, and Cosmetic Defect now requires one (docs/03-BUSINESS-RULES.md §3).
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(userId, new ListingDetailsInput(
                categoryId, null, conditionGradeId,
                "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null,
                AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
                null, null, null, null, []));
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            var addOption = await Listings.AddOptionAsync(userId, listingId, "Size");
            Assert.True(addOption.Succeeded, addOption.Error);
            var optionId = await Db.Set<ListingOption>()
                .Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();

            var addValue = await Listings.AddOptionValueAsync(userId, listingId, optionId, "M");
            Assert.True(addValue.Succeeded, addValue.Error);
            var valueId = await Db.Set<ListingOptionValue>()
                .Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();

            var addVariant = await Listings.AddVariantAsync(
                userId, listingId, new AddVariantInput("SNK-M", [valueId], initialQuantity));
            Assert.True(addVariant.Succeeded, addVariant.Error);

            var addImage = await Listings.AddImageAsync(userId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"));
            Assert.True(addImage.Succeeded, addImage.Error);

            var update = await Listings.UpdateDetailsAsync(userId, listingId, new ListingDetailsInput(
                categoryId, null, conditionGradeId,
                "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null,
                AllowB2C: true, AllowB2B: false, AllowMixedVariantB2B: false,
                null, null, null, null, [reasonId]));
            Assert.True(update.Succeeded, update.Error);

            _listingIds.Add(listingId);
            return listingId;
        }

        public async ValueTask DisposeAsync()
        {
            // Listings restrict-delete their Category/ConditionGrade/Brand/MerchantProfile
            // references (docs/04-DOMAIN-MODEL.md §12); a test listing left behind would stop
            // an unrelated catalog test elsewhere in this shared collection database from
            // deleting the category it used (docs/09-TEST-STRATEGY.md §2).
            if (_listingIds.Count > 0 || _merchantProfileIds.Count > 0)
            {
                await using var cleanupDb = CreateDbContext();

                if (_listingIds.Count > 0)
                {
                    var variantIds = await cleanupDb.ListingVariants
                        .Where(v => _listingIds.Contains(v.ListingId)).Select(v => v.Id).ToListAsync();
                    var adjustments = cleanupDb.InventoryAdjustments.Where(a => variantIds.Contains(a.ListingVariantId));
                    cleanupDb.InventoryAdjustments.RemoveRange(adjustments);
                    await cleanupDb.SaveChangesAsync();

                    var listings = await cleanupDb.Listings.Where(l => _listingIds.Contains(l.Id)).ToListAsync();
                    cleanupDb.Listings.RemoveRange(listings);
                    await cleanupDb.SaveChangesAsync();
                }

                if (_merchantProfileIds.Count > 0)
                {
                    var profiles = await cleanupDb.MerchantProfiles
                        .Where(p => _merchantProfileIds.Contains(p.Id)).ToListAsync();
                    cleanupDb.MerchantProfiles.RemoveRange(profiles);
                    await cleanupDb.SaveChangesAsync();
                }
            }

            _scope.Dispose();
        }
    }
}
