using System.Net;
using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The public marketplace routes exercised through the real MVC pipeline, not only the
/// service layer directly — proving the actual anonymous HTTP surface (routing, status codes,
/// the re-executed 404 page, and image serving) behaves as TASK-005 requires
/// (docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings").
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class PublicMarketplaceHttpTests(FaedWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
    });

    [SkippableFact]
    public async Task Shop_Anonymous_IsReachable()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await CreateClient().GetAsync("/shop");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task ListingDetail_UnknownSlug_Returns404_WithTheBrandedStatusPage()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await CreateClient().GetAsync($"/listing/{Guid.NewGuid():N}-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("couldn&#x27;t find that page", body, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task StoreFront_UnknownSlug_Returns404()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await CreateClient().GetAsync($"/store/{Guid.NewGuid():N}-does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task LiveListing_IsReachableByAnonymousHttp_AtItsDetailPage_StorePage_AndImage_UntilTheMerchantIsSuspended()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new HttpScope(factory);
        var (userId, merchantId) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        var merchantSlug = await scope.Db.MerchantProfiles.AsNoTracking()
            .Where(m => m.Id == merchantId).Select(m => m.PublicSlug).SingleAsync();
        var mediaId = await scope.Db.ListingMedia.AsNoTracking()
            .Where(m => m.ListingId == listingId).Select(m => m.Id).FirstAsync();

        var client = CreateClient();

        var detail = await client.GetAsync($"/listing/{slug}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("Men&#x27;s Running Sneakers", await detail.Content.ReadAsStringAsync());

        var store = await client.GetAsync($"/store/{merchantSlug}");
        Assert.Equal(HttpStatusCode.OK, store.StatusCode);

        var image = await client.GetAsync($"/listing-images/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);

        // Suspend the merchant: the listing keeps Status == Live, but every anonymous HTTP
        // surface must stop serving it (the P1 regression this proves end-to-end).
        var profile = await scope.Db.MerchantProfiles.SingleAsync(p => p.Id == merchantId);
        profile.Suspend(adminId, "Compliance hold", DateTime.UtcNow);
        await scope.Db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/listing/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/store/{merchantSlug}")).StatusCode);

        // A private image is now indistinguishable from one that does not exist: an
        // unauthorized caller and a bogus id both get 404, so probing never confirms the
        // image exists (docs/08-SECURITY-AND-PRIVACY.md §9, TASK-011 finding 7).
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/listing-images/{mediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/listing-images/{Guid.NewGuid()}")).StatusCode);

        // The owning (now-suspended) merchant can still reach the image directly.
        var ownerClient = CreateClient();
        ownerClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/listing-images/{mediaId}")).StatusCode);
    }

    [SkippableFact]
    public async Task ListingDetail_ForALiveListingOutsideTheLaunchSector_Returns404()
    {
        // AGENTS.md §3: the launch-sector restriction must hold on the real HTTP detail route,
        // not only in the service — a Live listing under a future, non-Fashion-Overstock
        // category is a 404 at /listing/{slug}.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new HttpScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var futureRoot = new Category("Electronics", $"electronics-{Guid.NewGuid():N}", null, 99);
        var futureChild = new Category("Phones", $"phones-{Guid.NewGuid():N}", futureRoot.Id, 0);
        scope.Db.Categories.AddRange(futureRoot, futureChild);
        await scope.Db.SaveChangesAsync();
        scope.TrackCategory(futureRoot.Id);
        scope.TrackCategory(futureChild.Id);

        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);
        await scope.Db.Listings.Where(l => l.Id == listingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.CategoryId, futureChild.Id));

        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        Assert.Equal(ListingStatus.Live, await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync());

        var response = await CreateClient().GetAsync($"/listing/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Minimal mirror of <c>PublicMarketplaceServiceTests.MarketplaceScope</c> — only
    /// what an HTTP-level test needs (docs/09-TEST-STRATEGY.md §2).</summary>
    private sealed class HttpScope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];
        private readonly List<Guid> _categoryIds = [];

        public void TrackCategory(Guid categoryId) => _categoryIds.Add(categoryId);

        public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

        public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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

        public async Task<Guid> CreateSubmittableListingAsync(
            string userId, int initialQuantity = 5, Guid? categoryId = null)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var resolvedCategoryId = categoryId ?? referenceData.Categories[0].Id;
            var conditionGradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(userId, new ListingDetailsInput(
                resolvedCategoryId, null, conditionGradeId,
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
                userId, listingId, new AddVariantInput($"SNK-{Guid.NewGuid():N}", [valueId], initialQuantity));
            Assert.True(addVariant.Succeeded, addVariant.Error);

            var addImage = await Listings.AddImageAsync(userId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"));
            Assert.True(addImage.Succeeded, addImage.Error);

            var update = await Listings.UpdateDetailsAsync(userId, listingId, new ListingDetailsInput(
                resolvedCategoryId, null, conditionGradeId,
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
            if (_listingIds.Count > 0 || _merchantProfileIds.Count > 0 || _categoryIds.Count > 0)
            {
                await using var cleanupDb = new ApplicationDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseSqlServer(Db.Database.GetConnectionString()
                            ?? throw new InvalidOperationException("The test DbContext has no connection string."))
                        .Options);

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

                if (_categoryIds.Count > 0)
                {
                    // Children before parent: Category.Parent restrict-deletes a populated branch.
                    var categories = await cleanupDb.Categories
                        .Where(c => _categoryIds.Contains(c.Id))
                        .OrderByDescending(c => c.ParentCategoryId != null)
                        .ToListAsync();
                    cleanupDb.Categories.RemoveRange(categories);
                    await cleanupDb.SaveChangesAsync();
                }
            }

            _scope.Dispose();
        }
    }
}
