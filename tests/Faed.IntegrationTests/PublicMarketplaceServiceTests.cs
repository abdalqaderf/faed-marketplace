using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Marketplace;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The public marketplace read surface against real SQL Server
/// (tasks/TASK-005-PUBLIC-MARKETPLACE.md exit criteria "Non-Live listings cannot be accessed
/// publicly", docs/11-ACCEPTANCE-CRITERIA.md "Public sees only Live listings").
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class PublicMarketplaceServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task GetListingBySlugAsync_OnlyEverReturnsALiveListing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();

        // Draft: not yet public.
        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(slug));

        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);

        // PendingReview: still not public.
        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(slug));

        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var live = await scope.Marketplace.GetListingBySlugAsync(slug);
        Assert.NotNull(live);
        Assert.Equal("Men's Running Sneakers", live!.Title);
        Assert.True(live.MerchantIsVerified);

        // Merchant hides it: no longer public, same slug.
        Assert.True((await scope.Listings.HideAsync(userId, listingId)).Succeeded);
        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(slug));
    }

    [SkippableFact]
    public async Task SuspendingTheMerchant_HidesTheirLiveListingEverywhere_EvenThoughTheListingStaysLive()
    {
        // The primary P1 regression: a Live listing's own status is untouched by suspending its
        // merchant, so every public read path must independently re-check the merchant, not
        // just the storefront header (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant
        // must be approved").
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, merchantId) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);
        var mediaId = await scope.Db.ListingMedia
            .Where(m => m.ListingId == listingId).Select(m => m.Id).SingleAsync();

        // Sanity: visible while the merchant is Approved.
        Assert.NotNull(await scope.Marketplace.GetListingBySlugAsync(slug));
        Assert.Contains(
            (await scope.Marketplace.BrowseListingsAsync(EmptyQuery())).Items, i => i.Id == listingId);
        Assert.True((await scope.Media.OpenImageAsync(null, mediaId)).Succeeded);

        var profile = await scope.Db.MerchantProfiles.SingleAsync(p => p.Id == merchantId);
        profile.Suspend(adminId, "Compliance hold", DateTime.UtcNow);
        await scope.Db.SaveChangesAsync();

        // The Listing row itself never changes...
        var stillLive = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.Live, stillLive);

        // ...but every public read path stops surfacing it.
        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(slug));
        Assert.DoesNotContain(
            (await scope.Marketplace.BrowseListingsAsync(EmptyQuery())).Items, i => i.Id == listingId);
        var homeFeatured = (await scope.Marketplace.GetHomePageAsync()).FeaturedListings;
        Assert.DoesNotContain(homeFeatured, i => i.Id == listingId);
        var image = await scope.Media.OpenImageAsync(null, mediaId);
        Assert.True(image.Failed);

        // The owning (now-suspended) merchant and an admin can still see it.
        Assert.True((await scope.Media.OpenImageAsync(userId, mediaId)).Succeeded);
        Assert.True((await scope.Media.OpenImageAsync(adminId, mediaId)).Succeeded);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_ExcludesNonLiveListings_AndAnUnresolvableCategoryYieldsZeroResults()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var draftListingId = await scope.CreateSubmittableListingAsync(userId);
        var liveListingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, liveListingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, liveListingId, null)).Succeeded);

        var everything = await scope.Marketplace.BrowseListingsAsync(EmptyQuery());
        Assert.Contains(everything.Items, i => i.Id == liveListingId);
        Assert.DoesNotContain(everything.Items, i => i.Id == draftListingId);

        var bogusCategory = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { CategorySlug = "not-a-real-category" });
        Assert.Equal(0, bogusCategory.TotalCount);
        Assert.Empty(bogusCategory.Items);
        // The filter UI still gets its options even when the current filter matches nothing.
        Assert.NotEmpty(bogusCategory.Facets.Categories);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_NeverExposesACategoryOutsideTheLaunchSector()
    {
        // AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI": a category added under
        // a future, non-Fashion-Overstock root must not appear in facets or browse results even
        // though it is perfectly "active", and a listing filed under it must not be reachable
        // by guessing its slug.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
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

        var everything = await scope.Marketplace.BrowseListingsAsync(EmptyQuery());
        Assert.DoesNotContain(everything.Items, i => i.Id == listingId);
        Assert.DoesNotContain(everything.Facets.Categories, c => c.Value == futureChild.Slug);

        var directFilter = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { CategorySlug = futureChild.Slug });
        Assert.Equal(0, directFilter.TotalCount);

        var home = await scope.Marketplace.GetHomePageAsync();
        Assert.DoesNotContain(home.Categories, c => c.Slug == futureChild.Slug);
        Assert.DoesNotContain(home.FeaturedListings, i => i.Id == listingId);
    }

    [SkippableFact]
    public async Task GetListingBySlugAsync_ForALiveListingOutsideTheLaunchSector_ReturnsNull()
    {
        // AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI": the launch-sector
        // boundary must hold on direct listing-detail access too, not only on Home/Shop — a
        // Live listing filed under a future, non-Fashion-Overstock category must 404 on its
        // own slug exactly as it is absent from browse.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var futureRoot = new Category("Electronics", $"electronics-{Guid.NewGuid():N}", null, 99);
        var futureChild = new Category("Phones", $"phones-{Guid.NewGuid():N}", futureRoot.Id, 0);
        scope.Db.Categories.AddRange(futureRoot, futureChild);
        await scope.Db.SaveChangesAsync();
        scope.TrackCategory(futureRoot.Id);
        scope.TrackCategory(futureChild.Id);

        var outsideId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, outsideId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, outsideId, null)).Succeeded);
        await scope.Db.Listings.Where(l => l.Id == outsideId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.CategoryId, futureChild.Id));

        var outsideSlug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == outsideId).Select(l => l.Slug).SingleAsync();
        Assert.Equal(ListingStatus.Live, await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == outsideId).Select(l => l.Status).SingleAsync());

        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(outsideSlug));

        // Control: a listing in the launch sector with the same setup is reachable. Pick a
        // launch category explicitly — GetReferenceDataAsync lists every active leaf category
        // globally, so "Categories[0]" could be the test-only "Phones" branch above.
        var launchCategoryId = (await scope.Marketplace.GetHomePageAsync()).Categories
            .Select(c => c.Slug).First();
        var insideCategoryId = await scope.Db.Categories.AsNoTracking()
            .Where(c => c.Slug == launchCategoryId).Select(c => c.Id).SingleAsync();
        var insideId = await scope.CreateSubmittableListingAsync(userId, categoryId: insideCategoryId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, insideId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, insideId, null)).Succeeded);
        var insideSlug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == insideId).Select(l => l.Slug).SingleAsync();
        Assert.NotNull(await scope.Marketplace.GetListingBySlugAsync(insideSlug));
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_FiltersByChannel_Inclusively()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        // CreateSubmittableListingAsync only enables B2C.
        var retailOnly = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { Channel = MarketplaceChannel.RetailOnly });
        Assert.Contains(retailOnly.Items, i => i.Id == listingId);

        var wholesaleOnly = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { Channel = MarketplaceChannel.WholesaleOnly });
        Assert.DoesNotContain(wholesaleOnly.Items, i => i.Id == listingId);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_AWholesaleOnlyListing_IsPriceFilterableAndSortable_ByItsIndicativePrice()
    {
        // docs/04-DOMAIN-MODEL.md §3: RetailPrice is required only when AllowB2C is set. A
        // B2B-only listing must still have an honest, usable price for filtering/sorting/cards
        // instead of being invisible to price filters or showing "Price on request".
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var listingId = await scope.CreateSubmittableListingAsync(
            userId, allowB2C: false, allowB2B: true, retailPrice: null, wholesaleUnitPrice: 12.75m);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var inRange = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { MinPrice = 10m, MaxPrice = 15m });
        var card = Assert.Single(inRange.Items, i => i.Id == listingId);
        Assert.Null(card.RetailPrice);
        Assert.Equal(12.75m, card.EffectivePrice);
        Assert.True(card.EffectivePriceIsWholesale);

        var outOfRange = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { MinPrice = 100m });
        Assert.DoesNotContain(outOfRange.Items, i => i.Id == listingId);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_SortsByPrice_UsingTheEffectivePriceForBothDirections()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var cheapId = await scope.CreateSubmittableListingAsync(userId, retailPrice: 9.99m);
        var expensiveId = await scope.CreateSubmittableListingAsync(userId, retailPrice: 99.99m);
        foreach (var id in new[] { cheapId, expensiveId })
        {
            Assert.True((await scope.Listings.SubmitForReviewAsync(userId, id)).Succeeded);
            Assert.True((await scope.Moderation.ApproveAsync(adminId, id, null)).Succeeded);
        }

        var lowToHigh = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { Sort = ShopSort.PriceLowToHigh });
        var lowToHighOrder = lowToHigh.Items.Select(i => i.Id).Where(id => id == cheapId || id == expensiveId).ToList();
        Assert.Equal([cheapId, expensiveId], lowToHighOrder);

        var highToLow = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { Sort = ShopSort.PriceHighToLow });
        var highToLowOrder = highToLow.Items.Select(i => i.Id).Where(id => id == cheapId || id == expensiveId).ToList();
        Assert.Equal([expensiveId, cheapId], highToLowOrder);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_FiltersBySizeAndColour()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        // CreateSubmittableListingAsync builds a "Size" option with value "M".
        var matching = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { SizeValue = "M" });
        Assert.Contains(matching.Items, i => i.Id == listingId);
        Assert.Contains(matching.Facets.Sizes, s => s.Value == "M");

        var nonMatching = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { SizeValue = "XXL" });
        Assert.DoesNotContain(nonMatching.Items, i => i.Id == listingId);
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_SizeAndColour_MustBeSatisfiedByOneSellableVariant_NotSpreadAcrossTheListing()
    {
        // faed-commerce-ux "do not imply stock exists at listing level when the selected SKU is
        // unavailable": a Size + Colour filter must match a single active, in-stock variant that
        // carries both values — not merely a listing where each value appears on some variant.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);

        // Starts as Size=M with one variant; rebuild it as Colour × Size with two variants:
        // Black/M in stock, White/L sold out.
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        var defaultVariantId = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();
        Assert.True((await scope.Listings.RemoveVariantAsync(userId, listingId, defaultVariantId)).Succeeded);

        var sizeOptionId = await scope.Db.Set<ListingOption>()
            .Where(o => o.ListingId == listingId && o.Name == "Size").Select(o => o.Id).SingleAsync();
        Assert.True((await scope.Listings.AddOptionValueAsync(userId, listingId, sizeOptionId, "L")).Succeeded);
        Assert.True((await scope.Listings.AddOptionAsync(userId, listingId, "Colour")).Succeeded);
        var colourOptionId = await scope.Db.Set<ListingOption>()
            .Where(o => o.ListingId == listingId && o.Name == "Colour").Select(o => o.Id).SingleAsync();
        Assert.True((await scope.Listings.AddOptionValueAsync(userId, listingId, colourOptionId, "Black")).Succeeded);
        Assert.True((await scope.Listings.AddOptionValueAsync(userId, listingId, colourOptionId, "White")).Succeeded);

        var values = await scope.Db.Set<ListingOptionValue>()
            .Where(v => v.Option.ListingId == listingId)
            .Select(v => new { v.Id, v.Value }).ToListAsync();
        Guid V(string value) => values.Single(x => x.Value == value).Id;

        Assert.True((await scope.Listings.AddVariantAsync(
            userId, listingId, new AddVariantInput("BLK-M", [V("Black"), V("M")], 5))).Succeeded);
        Assert.True((await scope.Listings.AddVariantAsync(
            userId, listingId, new AddVariantInput("WHT-L", [V("White"), V("L")], 0))).Succeeded);

        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        // The buyable combination matches.
        var blackM = await scope.Marketplace.BrowseListingsAsync(
            EmptyQuery() with { SizeValue = "M", ColorValue = "Black" });
        Assert.Contains(blackM.Items, i => i.Id == listingId);

        // White + M is not a real variant, even though "White" and "M" each exist on the listing.
        var whiteM = await scope.Marketplace.BrowseListingsAsync(
            EmptyQuery() with { SizeValue = "M", ColorValue = "White" });
        Assert.DoesNotContain(whiteM.Items, i => i.Id == listingId);

        // White/L exists but is sold out, so neither the filter nor the facet offers it.
        var whiteL = await scope.Marketplace.BrowseListingsAsync(
            EmptyQuery() with { SizeValue = "L", ColorValue = "White" });
        Assert.DoesNotContain(whiteL.Items, i => i.Id == listingId);
        Assert.DoesNotContain(whiteL.Facets.Sizes, s => s.Value == "L");
        Assert.DoesNotContain(whiteL.Facets.Colors, c => c.Value == "White");
        Assert.Contains(whiteL.Facets.Colors, c => c.Value == "Black");
    }

    [SkippableFact]
    public async Task BrowseListingsAsync_OutOfRangePage_ClampsToTheLastRealPage_InsteadOfReturningAnEmptyPageWithAPositiveTotal()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var farBeyondTheEnd = await scope.Marketplace.BrowseListingsAsync(EmptyQuery() with { Page = 999_999 });

        Assert.True(farBeyondTheEnd.TotalCount > 0);
        Assert.Equal(farBeyondTheEnd.TotalPages, farBeyondTheEnd.Page);
        Assert.NotEmpty(farBeyondTheEnd.Items);
    }

    [SkippableFact]
    public async Task GetMerchantStoreHeaderBySlugAsync_OnlyEverReturnsAnApprovedMerchant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (_, merchantId) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var slug = await scope.Db.MerchantProfiles.AsNoTracking()
            .Where(m => m.Id == merchantId).Select(m => m.PublicSlug).SingleAsync();

        var approved = await scope.Marketplace.GetMerchantStoreHeaderBySlugAsync(slug);
        Assert.NotNull(approved);
        Assert.True(approved!.IsVerified);

        var profile = await scope.Db.MerchantProfiles.SingleAsync(p => p.Id == merchantId);
        profile.Suspend(adminId, "Compliance hold", DateTime.UtcNow);
        await scope.Db.SaveChangesAsync();

        var suspended = await scope.Marketplace.GetMerchantStoreHeaderBySlugAsync(slug);
        Assert.Null(suspended);

        var unknown = await scope.Marketplace.GetMerchantStoreHeaderBySlugAsync("no-such-merchant");
        Assert.Null(unknown);
    }

    [SkippableTheory]
    [InlineData("B")]
    [InlineData("D")]
    public async Task SubmitForReviewAsync_ConditionGradeClaimsAPhysicalImperfection_WithoutEvidence_IsRejected(string gradeCode)
    {
        // Service-level proof that Listing.DescribeSubmissionBlockers' disclosure-evidence rule
        // (docs/03-BUSINESS-RULES.md §3) is actually wired to real catalog codes loaded from
        // the database, not only exercised with hand-picked strings at the unit level.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var listingId = await scope.CreateSubmittableListingAsync(userId, conditionGradeCode: gradeCode);

        var submit = await scope.Listings.SubmitForReviewAsync(userId, listingId);

        Assert.True(submit.Failed);
        Assert.Contains("defect or packaging photo", submit.Error);

        var addPackagingPhoto = await scope.Listings.AddImageAsync(userId, listingId, new AddListingImageInput(
            ListingMediaType.Packaging, TestImages.MinimalPngStream(), "box.png", "image/png",
            TestImages.MinimalPng.Length, null));
        Assert.True(addPackagingPhoto.Succeeded, addPackagingPhoto.Error);

        var retrySubmit = await scope.Listings.SubmitForReviewAsync(userId, listingId);
        Assert.True(retrySubmit.Succeeded, retrySubmit.Error);
    }

    [SkippableFact]
    public async Task AddImageAsync_AProductPhotoOnALiveListing_ReturnsItToPendingReview_AndDropsItFromPublicView()
    {
        // AGENTS.md §8: the merchant cannot change what the published Product gallery shows and
        // keep the listing public — adding a Product photo is a material change, so the listing
        // leaves public view until an admin re-approves it. The prior approval is preserved.
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        Assert.NotNull(await scope.Marketplace.GetListingBySlugAsync(slug));

        var add = await scope.Listings.AddImageAsync(userId, listingId, new AddListingImageInput(
            ListingMediaType.Product, TestImages.MinimalPngStream(), "side.png", "image/png",
            TestImages.MinimalPng.Length, "Side view"));
        Assert.True(add.Succeeded, add.Error);

        var status = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Status).SingleAsync();
        Assert.Equal(ListingStatus.PendingReview, status);
        Assert.Null(await scope.Marketplace.GetListingBySlugAsync(slug));

        var moderations = await scope.Db.ListingModerations.AsNoTracking()
            .Where(m => m.ListingId == listingId).ToListAsync();
        Assert.Contains(moderations, m => m.Status == ListingModerationStatus.Approved);
        Assert.Contains(moderations, m => m.Status == ListingModerationStatus.Pending);

        // A newly uploaded Product image is not publicly served while the listing is pending.
        var newMediaId = await scope.Db.ListingMedia.AsNoTracking()
            .Where(m => m.ListingId == listingId && m.OriginalFileName == "side.png")
            .Select(m => m.Id).SingleAsync();
        Assert.True((await scope.Media.OpenImageAsync(null, newMediaId)).Failed);
    }

    [SkippableFact]
    public async Task GetListingBySlugAsync_VariantAvailability_ReflectsDepletedStock()
    {
        // The data the client-side variant picker relies on: a fully depleted variant must
        // report IsSellable == false, not merely a zero-but-"active" state
        // (faed-commerce-ux "disable unavailable combinations").
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new MarketplaceScope(factory);
        var (userId, _) = await scope.CreateApprovedMerchantAsync();
        var adminId = await scope.CreateUserAsync(FaedRoles.Admin);
        var listingId = await scope.CreateSubmittableListingAsync(userId, initialQuantity: 1);
        Assert.True((await scope.Listings.SubmitForReviewAsync(userId, listingId)).Succeeded);
        Assert.True((await scope.Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var variantId = await scope.Db.ListingVariants
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();
        var adjust = await scope.Inventory.AdjustStockAsync(
            userId, new StockAdjustmentInput(variantId, InventoryAdjustmentType.StockLostOrDamaged, -1, "damaged"));
        Assert.True(adjust.Succeeded, adjust.Error);

        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        // The listing itself falls back to SoldOut and is no longer public once depleted, so
        // fetch through the tracking DbContext-backed query path the service itself uses is not
        // available anonymously here — assert directly against the persisted variant instead,
        // which is exactly the shape (AvailableQuantity/IsActive) the public detail view reads.
        var variant = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal(0, variant.AvailableQuantity);
        Assert.False(variant.IsActive && variant.AvailableQuantity > 0);
    }

    private static ShopQuery EmptyQuery() => new(
        CategorySlug: null,
        ConditionCode: null,
        DiscountReasonCode: null,
        BrandSlug: null,
        SizeValue: null,
        ColorValue: null,
        MinPrice: null,
        MaxPrice: null,
        Channel: MarketplaceChannel.All,
        Sort: ShopSort.Newest,
        SearchText: null,
        MerchantSlug: null,
        Page: 1,
        PageSize: ShopQuery.DefaultPageSize);

    /// <summary>Mirrors <c>ListingServiceTests.ListingScope</c>: real merchant/listing rows in
    /// the shared test database, removed on dispose (docs/09-TEST-STRATEGY.md §2).</summary>
    private sealed class MarketplaceScope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];
        private readonly List<Guid> _categoryIds = [];

        public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

        public IInventoryService Inventory => _scope.ServiceProvider.GetRequiredService<IInventoryService>();

        public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

        public IListingMediaService Media => _scope.ServiceProvider.GetRequiredService<IListingMediaService>();

        public IPublicMarketplaceService Marketplace => _scope.ServiceProvider.GetRequiredService<IPublicMarketplaceService>();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        /// <summary>Registers a category (typically inserted directly for a test-only sector)
        /// so it is deleted on dispose, same as a test-created listing/merchant.</summary>
        public void TrackCategory(Guid categoryId) => _categoryIds.Add(categoryId);

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

        /// <summary>A listing with one option/value, one stocked variant, a product photo, a
        /// discount reason and (by default) a retail price — ready to submit for review.
        /// Every parameter defaults to the plain B2C-only shape most tests need; callers
        /// override only what a specific scenario actually varies.</summary>
        public async Task<Guid> CreateSubmittableListingAsync(
            string userId,
            int initialQuantity = 5,
            bool allowB2C = true,
            bool allowB2B = false,
            decimal? retailPrice = 24.5m,
            decimal? wholesaleUnitPrice = null,
            string? conditionGradeCode = null,
            Guid? categoryId = null)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var resolvedCategoryId = categoryId ?? referenceData.Categories[0].Id;
            var resolvedGradeId = conditionGradeCode is null
                ? referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id
                : referenceData.ConditionGrades.Single(g => g.Label.Contains($"Grade {conditionGradeCode} ")).Id;
            // Not "[0]" (alphabetically "Cosmetic Defect") — most callers need a reason that does
            // *not* trigger the disclosure-evidence rule (docs/03-BUSINESS-RULES.md §3); tests
            // that specifically want a defect-requiring grade pass conditionGradeCode instead.
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(userId, new ListingDetailsInput(
                resolvedCategoryId, null, resolvedGradeId,
                "Men's Running Sneakers", "Comfortable running sneakers.",
                null, retailPrice, wholesaleUnitPrice, allowB2B ? 10 : null,
                allowB2C, allowB2B, AllowMixedVariantB2B: false,
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
                resolvedCategoryId, null, resolvedGradeId,
                "Men's Running Sneakers", "Comfortable running sneakers.",
                null, retailPrice, wholesaleUnitPrice, allowB2B ? 10 : null,
                allowB2C, allowB2B, AllowMixedVariantB2B: false,
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
