using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Faed.Web.Tests.Services.Subscriptions;

/// <summary>
/// The publish gate (PHASE-PLAN.md Phase 1 test 3, rewritten for Phase 6; and Phase 6's own
/// quota and renewal tests): drafts are buildable before verification is approved, and
/// publishing is blocked by each of the gate's three independent conditions in turn — approved
/// verification, an active subscription, and headroom under the plan's quota — with its own
/// message. A lapsed subscription's listings also return on renewal without a manual restore
/// per listing, capped at the plan's current quota. Exercises
/// <see cref="MerchantListingService"/> and <see cref="SubscriptionService"/> together against
/// a real (in-memory) <see cref="ApplicationDbContext"/> — this is not a rowversion
/// concurrency assertion, which the InMemory provider does not support.
/// </summary>
public class PublishGateTests
{
    [Fact]
    public async Task UnapprovedMerchant_CanBuildDrafts_ButEachGateConditionBlocksPublishingInTurn()
    {
        var db = CreateDb();
        var clock = new TestClock(DateTime.UtcNow);
        const string userId = "gate-merchant-user";
        const string adminId = "gate-admin-user";

        var (category, grade, reason) = SeedLaunchCatalog(db);

        db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.test", CreatedAtUtc = clock.UtcNow });
        db.Users.Add(new ApplicationUser { Id = adminId, UserName = $"{adminId}@example.test", CreatedAtUtc = clock.UtcNow });

        // Registered and verification submitted, but not yet approved.
        var profile = new MerchantProfile(userId, "Gate Test Merchant", $"slug-{Guid.NewGuid():N}", clock.UtcNow);
        profile.AddDocument(
            MerchantVerificationDocumentType.CommercialRegistration, "key", "doc.pdf", "application/pdf", 10, clock.UtcNow);
        profile.SubmitForReview(clock.UtcNow);
        db.MerchantProfiles.Add(profile);
        await db.SaveChangesAsync();

        var subscriptions = new SubscriptionService(
            db, new ManualSubscriptionBilling(), new FakeAdminUserRoleService(), clock, NullLogger<SubscriptionService>.Instance);
        var listings = new MerchantListingService(
            db, new ThrowingFileStorage(), subscriptions, clock, Options.Create(new ListingOptions()),
            NullLogger<MerchantListingService>.Instance);

        // Drafts are creatable from registration (BUSINESS-MODEL.md §7.4) — verification
        // approval is not required yet.
        var created = await listings.CreateAsync(userId, new ListingDetailsInput(
            category.Id, grade.Id, "Test Kettle", "A test description.", null, 25m, null,
            WarrantyType.None, null, null, null, [reason.Id]));
        Assert.True(created.Succeeded);
        var listingId = created.Value;

        // Complete the aggregate directly (no file-storage fake needed) so the only thing left
        // to block submission from here on is the publish gate, not an unrelated blocker.
        var listing = await db.Listings.SingleAsync(l => l.Id == listingId);
        listing.AddVariant("SKU-KETTLE", [], 1, clock.UtcNow);
        listing.AddMedia(ListingMediaType.Product, "key-kettle", "photo.jpg", "image/jpeg", 100, null, clock.UtcNow);
        await db.SaveChangesAsync();

        // 1. Not verified: approval has not happened yet.
        var notVerified = await listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(notVerified.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, notVerified.ErrorKind);
        Assert.Contains("approved", notVerified.Error, StringComparison.OrdinalIgnoreCase);

        // 2. Approved, but not subscribed: no plan has been chosen yet.
        profile.Approve(adminId, clock.UtcNow);
        await db.SaveChangesAsync();

        var notSubscribed = await listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(notSubscribed.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, notSubscribed.ErrorKind);
        Assert.Contains("subscription", notSubscribed.Error, StringComparison.OrdinalIgnoreCase);

        // 3. Subscribed, but at quota: another of the merchant's listings already fills it.
        var plan = new SubscriptionPlan("Test", "Test Plan", 10m, activeListingQuota: 1, hasFeaturedPlacement: false, sortOrder: 1);
        db.SubscriptionPlans.Add(plan);
        var subscription = new MerchantSubscription(profile.Id, plan.Id, clock.UtcNow);
        subscription.Activate(adminId, "REF-1", clock.UtcNow);
        db.MerchantSubscriptions.Add(subscription);

        var filler = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "filler", clock.UtcNow);
        filler.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        filler.Approve(adminId, "ok", clock.UtcNow);
        db.Listings.Add(filler);
        await db.SaveChangesAsync();

        var atQuota = await listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(atQuota.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, atQuota.ErrorKind);
        Assert.Contains("plan limit", atQuota.Error, StringComparison.OrdinalIgnoreCase);

        // All three conditions finally met: verified, subscribed, and — once the filler is
        // paused — under quota.
        Assert.True((await listings.HideAsync(userId, filler.Id)).Succeeded);
        var allowed = await listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public async Task MerchantAtQuota_CannotSubmit_ButCanAfterPausingOne()
    {
        var db = CreateDb();
        var clock = new TestClock(DateTime.UtcNow);
        const string userId = "merchant-user";
        const string adminId = "admin-user";

        var (category, grade, reason) = SeedCatalog(db);
        var profile = await CreateApprovedMerchantAsync(db, clock, userId, adminId, "Test Merchant");

        // A quota of one: the merchant can have exactly one listing live at a time.
        var plan = new SubscriptionPlan("Test", "Test Plan", 10m, activeListingQuota: 1, hasFeaturedPlacement: false, sortOrder: 1);
        db.SubscriptionPlans.Add(plan);

        var subscription = new MerchantSubscription(profile.Id, plan.Id, clock.UtcNow);
        subscription.Activate(adminId, "REF-1", clock.UtcNow);
        db.MerchantSubscriptions.Add(subscription);

        var liveListing = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "kettle", clock.UtcNow);
        liveListing.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        liveListing.Approve(adminId, "Looks good.", clock.UtcNow);
        db.Listings.Add(liveListing);

        var draftListing = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "toaster", clock.UtcNow);
        db.Listings.Add(draftListing);

        await db.SaveChangesAsync();

        var subscriptions = new SubscriptionService(
            db, new ManualSubscriptionBilling(), new FakeAdminUserRoleService(), clock, NullLogger<SubscriptionService>.Instance);
        var listings = new MerchantListingService(
            db, new ThrowingFileStorage(), subscriptions, clock, Options.Create(new ListingOptions()),
            NullLogger<MerchantListingService>.Instance);

        var blocked = await listings.SubmitForReviewAsync(userId, draftListing.Id);
        Assert.True(blocked.Failed);
        Assert.Equal(ResultErrorKind.Forbidden, blocked.ErrorKind);
        Assert.Contains("plan limit", blocked.Error, StringComparison.OrdinalIgnoreCase);

        var paused = await listings.HideAsync(userId, liveListing.Id);
        Assert.True(paused.Succeeded);

        var allowed = await listings.SubmitForReviewAsync(userId, draftListing.Id);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public async Task Renewal_RestoresLapsedListings_NewestFirst_CappedAtQuota()
    {
        var db = CreateDb();
        var clock = new TestClock(DateTime.UtcNow);
        const string userId = "renewal-merchant-user";
        const string adminId = "renewal-admin-user";

        var (category, grade, reason) = SeedCatalog(db);
        var profile = await CreateApprovedMerchantAsync(db, clock, userId, adminId, "Renewal Test Merchant");

        var bigPlan = new SubscriptionPlan("Big", "Big Plan", 50m, activeListingQuota: 3, hasFeaturedPlacement: false, sortOrder: 1);
        var smallPlan = new SubscriptionPlan("Small", "Small Plan", 20m, activeListingQuota: 2, hasFeaturedPlacement: false, sortOrder: 2);
        db.SubscriptionPlans.AddRange(bigPlan, smallPlan);

        var subscription = new MerchantSubscription(profile.Id, bigPlan.Id, clock.UtcNow);
        subscription.Activate(adminId, "REF-INITIAL", clock.UtcNow);
        db.MerchantSubscriptions.Add(subscription);

        // Three listings, oldest to newest, all live under the 3-slot plan.
        var oldest = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "oldest", clock.UtcNow);
        oldest.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        oldest.Approve(adminId, "ok", clock.UtcNow);
        db.Listings.Add(oldest);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var middle = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "middle", clock.UtcNow);
        middle.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        middle.Approve(adminId, "ok", clock.UtcNow);
        db.Listings.Add(middle);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var newest = BuildSubmittableDraft(profile.Id, category.Id, grade.Id, reason.Id, "newest", clock.UtcNow);
        newest.SubmitForReview("A", ["Overstock"], clock.UtcNow);
        newest.Approve(adminId, "ok", clock.UtcNow);
        db.Listings.Add(newest);

        var expiresAtUtc = subscription.ExpiresAtUtc!.Value;
        await db.SaveChangesAsync();

        var subscriptions = new SubscriptionService(
            db, new ManualSubscriptionBilling(), new FakeAdminUserRoleService(), clock, NullLogger<SubscriptionService>.Instance);

        // The subscription lapses: the sweep hides every one of the merchant's live listings.
        clock.UtcNow = expiresAtUtc.AddDays(1);
        var expiredCount = await subscriptions.ExpireDueSubscriptionsAsync();
        Assert.Equal(1, expiredCount);

        var lapsed = await subscriptions.GetMySubscriptionAsync(userId);
        Assert.Equal(0, lapsed!.Subscription!.LiveListingCount);

        // While lapsed, the merchant switches to the smaller plan — allowed, and nothing to
        // pause since nothing of theirs is currently live.
        var changedPlan = await subscriptions.ChoosePlanAsync(userId, smallPlan.Id);
        Assert.True(changedPlan.Succeeded);

        // Renewal: one admin action, no manual restore per listing. Only two of the three
        // lapsed listings fit under the smaller plan's quota.
        var activated = await subscriptions.ActivateAsync(adminId, profile.Id, "REF-RENEWAL");
        Assert.True(activated.Succeeded);

        var renewed = await subscriptions.GetMySubscriptionAsync(userId);
        Assert.Equal(2, renewed!.Subscription!.LiveListingCount);

        var refreshedOldest = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == oldest.Id);
        var refreshedMiddle = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == middle.Id);
        var refreshedNewest = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == newest.Id);

        // The two most recently published come back on their own; the oldest stays paused,
        // still flagged, for the merchant to unpause once they have room.
        Assert.True(refreshedMiddle.Status is ListingStatus.Live or ListingStatus.SoldOut);
        Assert.True(refreshedNewest.Status is ListingStatus.Live or ListingStatus.SoldOut);
        Assert.Equal(ListingStatus.Hidden, refreshedOldest.Status);
        Assert.True(refreshedOldest.HiddenBySubscriptionLapse);
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (Category Category, ConditionGrade Grade, DiscountReason Reason) SeedCatalog(ApplicationDbContext db)
    {
        var category = new Category("Small Kitchen Appliances", $"category-{Guid.NewGuid():N}", null, 0);
        var grade = new ConditionGrade("A", "Sealed", "New and unopened in the original box.", 1);
        var reason = new DiscountReason("Overstock", "Overstock");
        db.Categories.Add(category);
        db.ConditionGrades.Add(grade);
        db.DiscountReasons.Add(reason);
        return (category, grade, reason);
    }

    /// <summary>
    /// As <see cref="SeedCatalog"/>, but the category is a real child of the seeded launch
    /// root — needed only when a test goes through <see cref="MerchantListingService.CreateAsync"/>,
    /// which validates the category against <see cref="Faed.Web.Services.Catalog.LaunchCatalogScope"/>.
    /// </summary>
    private static (Category Category, ConditionGrade Grade, DiscountReason Reason) SeedLaunchCatalog(ApplicationDbContext db)
    {
        var root = new Category("Open-Box & Ex-Display", CatalogDataSeeder.RootCategorySlug, null, 0);
        var category = new Category("Small Kitchen Appliances", $"category-{Guid.NewGuid():N}", root.Id, 1);
        var grade = new ConditionGrade("A", "Sealed", "New and unopened in the original box.", 1);
        var reason = new DiscountReason("Overstock", "Overstock");
        db.Categories.AddRange(root, category);
        db.ConditionGrades.Add(grade);
        db.DiscountReasons.Add(reason);
        return (category, grade, reason);
    }

    private static async Task<MerchantProfile> CreateApprovedMerchantAsync(
        ApplicationDbContext db, TestClock clock, string userId, string adminId, string businessName)
    {
        db.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.test", CreatedAtUtc = clock.UtcNow });
        db.Users.Add(new ApplicationUser { Id = adminId, UserName = $"{adminId}@example.test", CreatedAtUtc = clock.UtcNow });

        var profile = new MerchantProfile(userId, businessName, $"slug-{Guid.NewGuid():N}", clock.UtcNow);
        profile.AddDocument(
            MerchantVerificationDocumentType.CommercialRegistration, "key", "doc.pdf", "application/pdf", 10, clock.UtcNow);
        profile.SubmitForReview(clock.UtcNow);
        profile.Approve(adminId, clock.UtcNow);
        db.MerchantProfiles.Add(profile);

        await Task.CompletedTask;
        return profile;
    }

    /// <summary>Builds a Draft listing with every field <c>DescribeSubmissionBlockers</c> requires.</summary>
    private static Listing BuildSubmittableDraft(
        Guid merchantProfileId, Guid categoryId, Guid conditionGradeId, Guid discountReasonId, string slug, DateTime nowUtc)
    {
        var listing = new Listing(
            merchantProfileId, categoryId, conditionGradeId, $"Test {slug}", $"test-{slug}-{Guid.NewGuid():N}", "A test description.", nowUtc);
        listing.AddVariant($"SKU-{slug}", [], initialQuantity: 1, nowUtc);
        listing.AddMedia(ListingMediaType.Product, $"key-{slug}", "photo.jpg", "image/jpeg", 100, null, nowUtc);
        listing.UpdateDetails(
            categoryId, conditionGradeId, $"Test {slug}", "A test description.", referencePrice: null, retailPrice: 25m,
            returnPolicyText: null, warrantyType: WarrantyType.None, warrantyMonths: null, includedItemsText: null,
            missingItemsText: null, discountReasonIds: [discountReasonId], nowUtc);
        return listing;
    }

    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; set; } = now;
    }

    /// <summary>
    /// Treats every caller as holding whatever role is asked for. <see cref="SubscriptionService"/>
    /// only ever asks this for its own admin-action defence-in-depth check, which every admin
    /// id used in these tests should pass.
    /// </summary>
    private sealed class FakeAdminUserRoleService : IUserRoleService
    {
        public Task AddToRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveFromRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class ThrowingFileStorage : IFileStorage
    {
        public Task<string> SaveAsync(
            string container, Stream content, string originalFileName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
