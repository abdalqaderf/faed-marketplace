using System.Net;
using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Ordering;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The B2C order routes through the real MVC pipeline (tasks/TASK-006-B2C-ORDERS.md
/// "unauthorized order access blocked"; docs/16-PERMISSIONS-MATRIX.md). Authorization and
/// ownership are asserted at the HTTP surface, not only at the service layer.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class OrderHttpTests(FaedWebApplicationFactory factory)
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient As(string userId)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        return client;
    }

    [SkippableFact]
    public async Task Checkout_Anonymous_IsChallenged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await Anonymous().GetAsync("/Buyer/Checkout?slug=anything");
        // The test auth scheme returns a clean 401 for an unauthenticated request.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task BuyerRoutes_AreForbiddenToAdministrators()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var admin = Anonymous();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        admin.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/Buyer/Checkout?slug=anything")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/Buyer/Orders")).StatusCode);
    }

    [SkippableFact]
    public async Task BuyerOrderDetails_ForSomeoneElsesOrder_Returns404()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new Scope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var variantId = await scope.CreateLiveListingVariantAsync(merchantUserId);
        var buyerId = await scope.CreateUserAsync();
        var otherUserId = Guid.NewGuid().ToString();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 1)],
            OrderFulfillmentType.Pickup, locationId, null, null, "Buyer", "079", null));
        Assert.True(placed.Succeeded, placed.Error);

        var ownerResponse = await As(buyerId).GetAsync($"/Buyer/Orders/Details/{placed.Value}");
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);

        var strangerResponse = await As(otherUserId).GetAsync($"/Buyer/Orders/Details/{placed.Value}");
        Assert.Equal(HttpStatusCode.NotFound, strangerResponse.StatusCode);
    }

    [SkippableFact]
    public async Task MerchantOrderPages_RenderForTheOwner_ButAnotherMerchantGets404OnTheDetail()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new Scope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (otherMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var locationId = await scope.AddPickupLocationAsync(merchantUserId);
        var variantId = await scope.CreateLiveListingVariantAsync(merchantUserId);
        var buyerId = await scope.CreateUserAsync();

        var placed = await scope.Orders.PlaceOrderAsync(buyerId, new PlaceOrderInput(
            [new OrderLineInput(variantId, 1)],
            OrderFulfillmentType.Pickup, locationId, null, null, "Buyer", "079", null));
        Assert.True(placed.Succeeded, placed.Error);

        var queue = await As(merchantUserId).GetAsync("/Merchant/Orders");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);

        var settings = await As(merchantUserId).GetAsync("/Merchant/StoreSettings");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var ownerDetail = await As(merchantUserId).GetAsync($"/Merchant/Orders/Details/{placed.Value}");
        Assert.Equal(HttpStatusCode.OK, ownerDetail.StatusCode);
        Assert.Contains("Confirm order", await ownerDetail.Content.ReadAsStringAsync());

        var strangerDetail = await As(otherMerchantUserId).GetAsync($"/Merchant/Orders/Details/{placed.Value}");
        Assert.Equal(HttpStatusCode.NotFound, strangerDetail.StatusCode);
    }

    [SkippableFact]
    public async Task Checkout_RendersTheOrderBuilder_ForASignedInBuyer()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new Scope(factory);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        await scope.AddPickupLocationAsync(merchantUserId);
        var variantId = await scope.CreateLiveListingVariantAsync(merchantUserId);
        var listingId = await scope.Db.ListingVariants.AsNoTracking()
            .Where(v => v.Id == variantId).Select(v => v.ListingId).SingleAsync();
        var slug = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();

        var response = await As(Guid.NewGuid().ToString()).GetAsync($"/Buyer/Checkout?slug={slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Place order", body);
    }

    private sealed class Scope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];

        public IOrderService Orders => _scope.ServiceProvider.GetRequiredService<IOrderService>();

        public IMerchantStoreService Store => _scope.ServiceProvider.GetRequiredService<IMerchantStoreService>();

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

        public async Task<Guid> AddPickupLocationAsync(string merchantUserId)
        {
            var result = await Store.AddLocationAsync(merchantUserId, new MerchantLocationInput(
                "Main store", "1 King St", "Abdali", "Amman", null, "Sun–Thu 10:00–18:00"));
            Assert.True(result.Succeeded, result.Error);
            return result.Value;
        }

        public async Task<Guid> CreateLiveListingVariantAsync(string merchantUserId)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var create = await Listings.CreateAsync(merchantUserId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null, true, false, false, null, null, null, null, []));
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
            Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, "M")).Succeeded);
            var valueId = await Db.Set<ListingOptionValue>().Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();
            Assert.True((await Listings.AddVariantAsync(
                merchantUserId, listingId, new AddVariantInput($"SNK-{Guid.NewGuid():N}", [valueId], 5))).Succeeded);
            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
            Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, new ListingDetailsInput(
                categoryId, null, gradeId, "Men's Running Sneakers", "Comfortable running sneakers.",
                null, 24.5m, null, null, true, false, false, null, null, null, null, [reasonId]))).Succeeded);
            Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
            var adminId = await CreateUserAsync(FaedRoles.Admin);
            Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

            _listingIds.Add(listingId);
            return await Db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).Select(v => v.Id).SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await using var cleanupDb = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(Db.Database.GetConnectionString()!)
                    .Options);

            var merchantIds = _merchantProfileIds;
            var listingIds = _listingIds;

            cleanupDb.Orders.RemoveRange(
                await cleanupDb.Orders.Where(o => merchantIds.Contains(o.MerchantProfileId)).ToListAsync());
            await cleanupDb.SaveChangesAsync();
            cleanupDb.MerchantLocations.RemoveRange(
                cleanupDb.MerchantLocations.Where(l => merchantIds.Contains(l.MerchantProfileId)));
            cleanupDb.MerchantDeliveryZones.RemoveRange(
                cleanupDb.MerchantDeliveryZones.Where(z => merchantIds.Contains(z.MerchantProfileId)));
            await cleanupDb.SaveChangesAsync();

            if (listingIds.Count > 0)
            {
                var variantIds = await cleanupDb.ListingVariants
                    .Where(v => listingIds.Contains(v.ListingId)).Select(v => v.Id).ToListAsync();
                cleanupDb.InventoryAdjustments.RemoveRange(
                    cleanupDb.InventoryAdjustments.Where(a => variantIds.Contains(a.ListingVariantId)));
                await cleanupDb.SaveChangesAsync();
                cleanupDb.Listings.RemoveRange(
                    await cleanupDb.Listings.Where(l => listingIds.Contains(l.Id)).ToListAsync());
                await cleanupDb.SaveChangesAsync();
            }

            cleanupDb.MerchantProfiles.RemoveRange(
                await cleanupDb.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)).ToListAsync());
            await cleanupDb.SaveChangesAsync();

            _scope.Dispose();
        }
    }
}
