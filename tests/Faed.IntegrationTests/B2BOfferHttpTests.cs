using System.Net;
using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The B2B offer routes through the real MVC pipeline (tasks/TASK-007-B2B-NEGOTIATION.md
/// "permission-safe"; docs/16-PERMISSIONS-MATRIX.md "Submit B2B offer — verified merchant
/// only", "View unrelated B2B negotiation — ❌"). Authorization and participation are
/// asserted at the HTTP surface, not only at the service layer.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class B2BOfferHttpTests(FaedWebApplicationFactory factory)
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient As(string userId, params string[] roles)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }
        return client;
    }

    [SkippableFact]
    public async Task OffersQueue_Anonymous_IsChallenged()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await Anonymous().GetAsync("/Merchant/Offers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task OffersQueue_ForAUserWithoutAnApprovedMerchantProfile_IsForbidden()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        var response = await As(Guid.NewGuid().ToString()).GetAsync("/Merchant/Offers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task OfferRoutes_ForAnApprovedMerchantWithAdminRole_AreForbiddenAtTheAuthorizationBoundary()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new Scope(factory);
        var (adminMerchantUserId, _) = await scope.CreateApprovedMerchantAsync();

        var response = await As(adminMerchantUserId, FaedRoles.Admin).GetAsync("/Merchant/Offers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task OfferPages_RenderForAParticipant_ButAnUnrelatedMerchantGets404OnTheDetail()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new Scope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (strangerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId);

        var create = await As(buyerUserId).GetAsync($"/Merchant/Offers/Create?listingSlug={slug}");
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Contains("wholesale offer", await create.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);

        Assert.Equal(HttpStatusCode.OK, (await As(sellerUserId).GetAsync("/Merchant/Offers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await As(sellerUserId).GetAsync($"/Merchant/Offers/Details/{start.Value}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await As(buyerUserId).GetAsync($"/Merchant/Offers/Details/{start.Value}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await As(strangerUserId).GetAsync($"/Merchant/Offers/Details/{start.Value}")).StatusCode);
    }

    private sealed class Scope(FaedWebApplicationFactory factory) : IAsyncDisposable
    {
        private readonly IServiceScope _scope = factory.Services.CreateScope();
        private readonly List<Guid> _listingIds = [];
        private readonly List<Guid> _merchantProfileIds = [];

        public IB2BNegotiationService Negotiations => _scope.ServiceProvider.GetRequiredService<IB2BNegotiationService>();

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

        public async Task<(string Slug, IReadOnlyList<Guid> VariantIds)> CreateLiveB2BListingAsync(string merchantUserId)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var details = new ListingDetailsInput(
                categoryId, null, gradeId, "Wholesale Hoodies", "Overstock fleece hoodies.",
                null, 12.000m, 7.000m, 10, true, true, true, null, null, null, null, []);

            var create = await Listings.CreateAsync(merchantUserId, details);
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();
            Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, "L")).Succeeded);
            var valueId = await Db.Set<ListingOptionValue>()
                .Where(v => v.ListingOptionId == optionId).Select(v => v.Id).SingleAsync();
            Assert.True((await Listings.AddVariantAsync(
                merchantUserId, listingId, new AddVariantInput($"HOD-{Guid.NewGuid():N}", [valueId], 30))).Succeeded);
            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
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

        public async ValueTask DisposeAsync()
        {
            await using var cleanupDb = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(Db.Database.GetConnectionString()!)
                    .Options);

            var merchantIds = _merchantProfileIds;
            var listingIds = _listingIds;

            cleanupDb.B2BNegotiations.RemoveRange(
                await cleanupDb.B2BNegotiations.Where(n => merchantIds.Contains(n.SellingMerchantProfileId)
                    || merchantIds.Contains(n.BuyingMerchantProfileId)).ToListAsync());
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
