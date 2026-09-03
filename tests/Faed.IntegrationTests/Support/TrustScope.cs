using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Analytics;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// Shared fixture helper for the trust (dispute + review) integration tests: builds approved
/// merchants, confirmed / completed B2C orders and completed B2B deals, and exposes the trust
/// services plus a gated <see cref="DisputeService"/> for deterministic concurrency tests.
/// </summary>
internal sealed class TrustScope(FaedWebApplicationFactory factory) : IAsyncDisposable
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();
    private readonly List<Guid> _merchantProfileIds = [];
    private readonly List<Guid> _listingIds = [];
    private readonly List<ApplicationDbContext> _extraContexts = [];

    public IServiceProvider Services => _scope.ServiceProvider;

    public IDisputeService Disputes => _scope.ServiceProvider.GetRequiredService<IDisputeService>();

    public IReviewService Reviews => _scope.ServiceProvider.GetRequiredService<IReviewService>();

    public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

    public IB2BNegotiationService Negotiations => _scope.ServiceProvider.GetRequiredService<IB2BNegotiationService>();

    public IB2BDealService Deals => _scope.ServiceProvider.GetRequiredService<IB2BDealService>();

    public IMerchantStoreService Store => _scope.ServiceProvider.GetRequiredService<IMerchantStoreService>();

    public IMerchantListingService Listings => _scope.ServiceProvider.GetRequiredService<IMerchantListingService>();

    public IMerchantAnalyticsService Analytics => _scope.ServiceProvider.GetRequiredService<IMerchantAnalyticsService>();

    public IListingModerationService Moderation => _scope.ServiceProvider.GetRequiredService<IListingModerationService>();

    public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>A fresh, independently-tracked context on the shared test connection.</summary>
    public ApplicationDbContext CreateDbContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(Db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The test DbContext has no connection string."))
            .Options);
        _extraContexts.Add(context);
        return context;
    }

    /// <summary>
    /// A <see cref="DisputeService"/> wired over a <see cref="GatedApplicationDbContext"/> so a
    /// test can interleave it deterministically with a competing filing.
    /// </summary>
    public DisputeService NewGatedDisputeService(Func<CancellationToken, Task> beforeFirstSave) => new(
        new GatedApplicationDbContext(CreateDbContext(), beforeFirstSave),
        _scope.ServiceProvider.GetRequiredService<IFileStorage>(),
        _scope.ServiceProvider.GetRequiredService<IUserRoleService>(),
        _scope.ServiceProvider.GetRequiredService<IClock>(),
        _scope.ServiceProvider.GetRequiredService<IOptions<TrustOptions>>(),
        _scope.ServiceProvider.GetRequiredService<ILogger<DisputeService>>());

    public async Task<string> CreateUserAsync(string? role = null)
    {
        var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = $"{Guid.NewGuid():N}@test.local",
            Email = $"{Guid.NewGuid():N}@test.local",
            EmailConfirmed = true,
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);

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

    private async Task<Guid> AddPickupLocationAsync(string merchantUserId)
    {
        var result = await Store.AddLocationAsync(merchantUserId, new MerchantLocationInput(
            "Main store", "1 King St", "Abdali", "Amman", "Ring the bell", "Sun–Thu 10:00–18:00"));
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    private async Task<(Guid ListingId, Guid VariantId)> CreateLiveB2CListingAsync(string merchantUserId)
    {
        var reference = await Listings.GetReferenceDataAsync();
        var categoryId = reference.Categories[0].Id;
        var gradeId = reference.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
        var reasonId = reference.DiscountReasons.Single(r => r.Label == "Overstock").Id;

        var details = new ListingDetailsInput(
            categoryId, null, gradeId, "Trust Test Jacket", "A jacket for trust tests.",
            null, 20.000m, null, null, true, false, false, null, null, null, null, []);
        var create = await Listings.CreateAsync(merchantUserId, details);
        Assert.True(create.Succeeded, create.Error);
        var listingId = create.Value;

        Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
        var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
        Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, "M")).Succeeded);
        var valueId = await Db.Set<ListingOptionValue>().Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();
        Assert.True((await Listings.AddVariantAsync(
            merchantUserId, listingId, new AddVariantInput($"JKT-{Guid.NewGuid():N}", [valueId], 10))).Succeeded);
        Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
            ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
            TestImages.MinimalPng.Length, "Front"))).Succeeded);
        Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, details with { DiscountReasonIds = [reasonId] })).Succeeded);
        Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
        var adminId = await CreateUserAsync(FaedRoles.Admin);
        Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var variantId = await Db.ListingVariants.AsNoTracking()
            .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();
        _listingIds.Add(listingId);
        return (listingId, variantId);
    }

    public async Task<(string BuyerUserId, Guid OrderId)> PlacePendingOrderAsync(string merchantUserId)
    {
        var locationId = await AddPickupLocationAsync(merchantUserId);
        var (_, variantId) = await CreateLiveB2CListingAsync(merchantUserId);
        var buyerUserId = await CreateUserAsync();

        var placed = await Orders.PlaceOrderAsync(buyerUserId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 1)],
            OrderFulfillmentType.Pickup, locationId, null, null, "Buyer One", "0790000000", null));
        Assert.True(placed.Succeeded, placed.Error);
        return (buyerUserId, placed.Value);
    }

    public async Task<(string BuyerUserId, Guid OrderId)> CreateConfirmedOrderAsync(string merchantUserId)
    {
        var (buyerUserId, orderId) = await PlacePendingOrderAsync(merchantUserId);
        Assert.True((await Orders.ConfirmAsync(merchantUserId, orderId)).Succeeded);
        return (buyerUserId, orderId);
    }

    public async Task CompleteOrderAsync(string merchantUserId, Guid orderId)
    {
        Assert.True((await Orders.MarkReadyForPickupAsync(merchantUserId, orderId)).Succeeded);
        Assert.True((await Orders.CompleteAsync(merchantUserId, orderId)).Succeeded);
    }

    private async Task<(string Slug, IReadOnlyList<Guid> VariantIds)> CreateLiveB2BListingAsync(string merchantUserId)
    {
        var reference = await Listings.GetReferenceDataAsync();
        var categoryId = reference.Categories[0].Id;
        var gradeId = reference.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
        var reasonId = reference.DiscountReasons.Single(r => r.Label == "Overstock").Id;

        var details = new ListingDetailsInput(
            categoryId, null, gradeId, "Trust Wholesale Tees", "Overstock tees.",
            null, 9.000m, 5.000m, 10, true, true, true, null, null, null, null, []);
        var create = await Listings.CreateAsync(merchantUserId, details);
        Assert.True(create.Succeeded, create.Error);
        var listingId = create.Value;

        Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
        var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
        Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, "M")).Succeeded);
        var valueId = await Db.Set<ListingOptionValue>().Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();
        Assert.True((await Listings.AddVariantAsync(
            merchantUserId, listingId, new AddVariantInput($"TEE-{Guid.NewGuid():N}", [valueId], 40))).Succeeded);
        Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
            ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
            TestImages.MinimalPng.Length, "Front"))).Succeeded);
        Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, details with { DiscountReasonIds = [reasonId] })).Succeeded);
        Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
        var adminId = await CreateUserAsync(FaedRoles.Admin);
        Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

        var variantIds = await Db.ListingVariants.AsNoTracking()
            .Where(v => v.ListingId == listingId).OrderBy(v => v.Sku).Select(v => v.Id).ToListAsync();
        var slug = await Db.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
        _listingIds.Add(listingId);
        return (slug, variantIds);
    }

    /// <summary>Places a B2C order then cancels it as the merchant, leaving a Live, never-sold listing.</summary>
    public async Task<(string BuyerUserId, Guid OrderId)> CreateCancelledOrderAsync(string merchantUserId)
    {
        var (buyerUserId, orderId) = await PlacePendingOrderAsync(merchantUserId);
        Assert.True((await Orders.CancelAsMerchantAsync(merchantUserId, orderId, "Analytics test cancel")).Succeeded);
        return (buyerUserId, orderId);
    }

    /// <summary>Opens a B2B negotiation on a fresh listing and leaves it Open (unaccepted).</summary>
    public async Task<Guid> StartOpenNegotiationAsync(string sellerUserId, string buyerUserId)
    {
        var (slug, variantIds) = await CreateLiveB2BListingAsync(sellerUserId);
        var start = await Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4.000m, null, null));
        Assert.True(start.Succeeded, start.Error);
        return start.Value;
    }

    public async Task<Guid> CreateCompletedDealAsync(string sellerUserId, string buyerUserId)
    {
        var (slug, variantIds) = await CreateLiveB2BListingAsync(sellerUserId);
        var start = await Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 12)], 4.000m, null, null));
        Assert.True(start.Succeeded, start.Error);

        var accept = await Deals.AcceptOfferAsync(sellerUserId, start.Value, new AcceptOfferInput(B2BFulfillmentType.Pickup));
        Assert.True(accept.Succeeded, accept.Error);

        Assert.True((await Deals.MarkReadyForPickupAsync(sellerUserId, accept.Value)).Succeeded);
        Assert.True((await Deals.MarkDeliveredAsync(sellerUserId, accept.Value)).Succeeded);
        Assert.True((await Deals.CompleteAsync(buyerUserId, accept.Value)).Succeeded);
        return accept.Value;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _extraContexts)
        {
            await context.DisposeAsync();
        }

        await using var cleanup = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(Db.Database.GetConnectionString()!)
                .Options);

        var merchantIds = _merchantProfileIds;
        var listingIds = _listingIds;

        var orderIds = await cleanup.Orders.Where(o => merchantIds.Contains(o.MerchantProfileId))
            .Select(o => o.Id).ToListAsync();
        var dealIds = await cleanup.B2BDeals.Where(d =>
                merchantIds.Contains(d.SellingMerchantProfileId) || merchantIds.Contains(d.BuyingMerchantProfileId))
            .Select(d => d.Id).ToListAsync();
        var disputeIds = await cleanup.Disputes.Where(d =>
                (d.OrderId != null && orderIds.Contains(d.OrderId.Value))
                || (d.B2BDealId != null && dealIds.Contains(d.B2BDealId.Value)))
            .Select(d => d.Id).ToListAsync();

        cleanup.DisputeEvidence.RemoveRange(
            await cleanup.DisputeEvidence.Where(e => disputeIds.Contains(e.DisputeId)).ToListAsync());
        await cleanup.SaveChangesAsync();
        cleanup.Disputes.RemoveRange(
            await cleanup.Disputes.Where(d => disputeIds.Contains(d.Id)).ToListAsync());
        cleanup.Reviews.RemoveRange(
            await cleanup.Reviews.Where(r => merchantIds.Contains(r.ReviewedMerchantProfileId)).ToListAsync());
        await cleanup.SaveChangesAsync();

        cleanup.B2BDeals.RemoveRange(await cleanup.B2BDeals.Where(d =>
            merchantIds.Contains(d.SellingMerchantProfileId) || merchantIds.Contains(d.BuyingMerchantProfileId)).ToListAsync());
        await cleanup.SaveChangesAsync();
        cleanup.B2BNegotiations.RemoveRange(await cleanup.B2BNegotiations.Where(n =>
            merchantIds.Contains(n.SellingMerchantProfileId) || merchantIds.Contains(n.BuyingMerchantProfileId)).ToListAsync());
        await cleanup.SaveChangesAsync();
        cleanup.Orders.RemoveRange(await cleanup.Orders.Where(o => merchantIds.Contains(o.MerchantProfileId)).ToListAsync());
        await cleanup.SaveChangesAsync();
        cleanup.MerchantLocations.RemoveRange(cleanup.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)));
        await cleanup.SaveChangesAsync();

        if (listingIds.Count > 0)
        {
            var variantIds = await cleanup.ListingVariants.Where(v => listingIds.Contains(v.ListingId)).Select(v => v.Id).ToListAsync();
            cleanup.InventoryAdjustments.RemoveRange(cleanup.InventoryAdjustments.Where(a => variantIds.Contains(a.ListingVariantId)));
            await cleanup.SaveChangesAsync();
            cleanup.Listings.RemoveRange(await cleanup.Listings.Where(l => listingIds.Contains(l.Id)).ToListAsync());
            await cleanup.SaveChangesAsync();
        }

        cleanup.MerchantProfiles.RemoveRange(await cleanup.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)).ToListAsync());
        await cleanup.SaveChangesAsync();

        _scope.Dispose();
    }
}
