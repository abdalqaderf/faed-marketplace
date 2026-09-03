using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.B2B;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// B2B negotiation against real SQL Server (tasks/TASK-007-B2B-NEGOTIATION.md "Critical
/// rules" / "Exit criteria"; docs/09-TEST-STRATEGY.md §3 "B2B negotiation"). The accepted
/// deal and its atomic stock reservation are TASK-008 — acceptance here only records the
/// agreed revision (docs/adr/0004).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class B2BNegotiationServiceTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task StartNegotiation_OnYourOwnListing_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var result = await scope.Negotiations.StartNegotiationAsync(sellerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));

        Assert.True(result.Failed);
        Assert.Empty(await scope.Db.B2BNegotiations.AsNoTracking().ToListAsync());
    }

    [SkippableFact]
    public async Task StartNegotiation_ThenACounterOfferChain_PersistsEveryRevisionImmutably()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4.000m, "Opening offer", null));
        Assert.True(start.Succeeded, start.Error);
        var id = start.Value;

        Assert.True((await scope.Negotiations.CounterOfferAsync(sellerUserId, id, new CounterOfferInput(
            [new B2BOfferLineInput(variantIds[0], 10)], 5.500m, "Meet in the middle", null))).Succeeded);
        Assert.True((await scope.Negotiations.CounterOfferAsync(buyerUserId, id, new CounterOfferInput(
            [new B2BOfferLineInput(variantIds[0], 12)], 5.000m, null, null))).Succeeded);

        var negotiation = await scope.Db.B2BNegotiations.AsNoTracking()
            .Include(n => n.Revisions).ThenInclude(r => r.Lines)
            .SingleAsync(n => n.Id == id);

        Assert.Equal(3, negotiation.CurrentRevisionNumber);
        Assert.Equal([1, 2, 3], negotiation.Revisions.OrderBy(r => r.RevisionNumber).Select(r => r.RevisionNumber));

        var first = negotiation.Revisions.Single(r => r.RevisionNumber == 1);
        Assert.Equal(4.000m, first.ProposedUnitPrice);
        Assert.Equal(40.000m, first.ProposedTotal);
        Assert.Equal("Opening offer", first.Message);

        var third = negotiation.Revisions.Single(r => r.RevisionNumber == 3);
        Assert.Equal(5.000m, third.ProposedUnitPrice);
        Assert.Equal(12, third.Lines.Single().Quantity);
    }

    [SkippableFact]
    public async Task Counter_AlternatesSides_AndAMerchantCannotAcceptItsOwnOffer()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var id = start.Value;

        // The buyer proposed revision 1 — it is not the buyer's turn to counter again.
        Assert.True((await scope.Negotiations.CounterOfferAsync(buyerUserId, id, new CounterOfferInput(
            [new B2BOfferLineInput(variantIds[0], 10)], 4.5m, null, null))).Failed);

        Assert.True((await scope.Negotiations.CounterOfferAsync(sellerUserId, id, new CounterOfferInput(
            [new B2BOfferLineInput(variantIds[0], 10)], 6m, null, null))).Succeeded);

        // The seller made revision 2; the seller cannot accept its own offer.
        Assert.True((await scope.Negotiations.AcceptAsync(sellerUserId, id)).Failed);

        Assert.True((await scope.Negotiations.AcceptAsync(buyerUserId, id)).Succeeded);
        var status = await scope.Db.B2BNegotiations.AsNoTracking()
            .Where(n => n.Id == id).Select(n => n.Status).SingleAsync();
        Assert.Equal(B2BNegotiationStatus.Accepted, status);
    }

    [SkippableFact]
    public async Task AcceptingAnExpiredOffer_IsRejected_AndSynchronouslyExpiresTheNegotiation()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var id = start.Value;

        await scope.Db.B2BOfferRevisions
            .Where(r => r.B2BNegotiationId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.OfferExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        // A fresh scope reloads the negotiation from the database rather than a stale tracked
        // graph — the same pattern the B2C expiry tests use.
        await using (var acceptScope = new NegotiationScope(factory))
        {
            var accept = await acceptScope.Negotiations.AcceptAsync(sellerUserId, id);
            Assert.True(accept.Failed);
            Assert.Equal(ResultErrorKind.Conflict, accept.ErrorKind);

            var finalStatus = await acceptScope.Db.B2BNegotiations.AsNoTracking()
                .Where(n => n.Id == id).Select(n => n.Status).SingleAsync();
            Assert.Equal(B2BNegotiationStatus.Expired, finalStatus);
            Assert.Equal(0, await acceptScope.Negotiations.ExpireLapsedNegotiationsAsync());
        }
    }

    [SkippableFact]
    public async Task CounteringOrRejectingAnExpiredOffer_IsBlockedAndSynchronouslyExpiresIt()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var counterTarget = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var rejectTarget = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[1], 10)], 4m, null, null));

        var ids = new[] { counterTarget.Value, rejectTarget.Value };
        await scope.Db.B2BOfferRevisions
            .Where(r => ids.Contains(r.B2BNegotiationId))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.OfferExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        await using var actionScope = new NegotiationScope(factory);
        var counter = await actionScope.Negotiations.CounterOfferAsync(
            sellerUserId,
            counterTarget.Value,
            new CounterOfferInput([new B2BOfferLineInput(variantIds[0], 10)], 4.500m, null, null));
        var reject = await actionScope.Negotiations.RejectAsync(sellerUserId, rejectTarget.Value);

        Assert.Equal(ResultErrorKind.Conflict, counter.ErrorKind);
        Assert.Equal(ResultErrorKind.Conflict, reject.ErrorKind);

        var persisted = await actionScope.Db.B2BNegotiations.AsNoTracking()
            .Include(n => n.Revisions)
            .Where(n => ids.Contains(n.Id))
            .ToListAsync();
        Assert.All(persisted, n => Assert.Equal(B2BNegotiationStatus.Expired, n.Status));
        Assert.All(persisted, n => Assert.Single(n.Revisions));
    }

    [SkippableFact]
    public async Task ANegotiationIsInvisibleAndUntouchableByAMerchantThatIsNotAParticipant()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (strangerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var id = start.Value;

        Assert.NotNull(await scope.Negotiations.GetNegotiationAsync(sellerUserId, id));
        Assert.NotNull(await scope.Negotiations.GetNegotiationAsync(buyerUserId, id));
        Assert.Null(await scope.Negotiations.GetNegotiationAsync(strangerUserId, id));

        var act = await scope.Negotiations.AcceptAsync(strangerUserId, id);
        Assert.True(act.Failed);
        Assert.Equal(ResultErrorKind.NotFound, act.ErrorKind);

        var counter = await scope.Negotiations.CounterOfferAsync(strangerUserId, id, new CounterOfferInput(
            [new B2BOfferLineInput(variantIds[0], 10)], 3m, null, null));
        Assert.Equal(ResultErrorKind.NotFound, counter.ErrorKind);
    }

    [SkippableFact]
    public async Task AcceptingAnOffer_RecordsTheAgreement_ButReservesNoStock()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10, initialQuantity: 40);

        var before = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 25)], 4m, null, null));
        Assert.True((await scope.Negotiations.AcceptAsync(sellerUserId, start.Value)).Succeeded);

        var after = await scope.Db.ListingVariants.AsNoTracking().SingleAsync(v => v.Id == variantIds[0]);
        Assert.Equal(before.AvailableQuantity, after.AvailableQuantity);
        Assert.Equal(0, after.ReservedQuantity);
        Assert.Equal(0, after.SoldQuantity);
    }

    [SkippableFact]
    public async Task StartNegotiation_BelowTheListingMinimumOrderQuantity_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var result = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 4)], 4m, null, null));

        Assert.True(result.Failed);
        Assert.Empty(await scope.Db.B2BNegotiations.AsNoTracking().ToListAsync());
    }

    [SkippableFact]
    public async Task OfferPrices_RequireThreeDecimalJodPrecision_AndPersistWithConsistentServerTotals()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var invalidStart = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 1.2345m, null, null));
        Assert.Equal(ResultErrorKind.Validation, invalidStart.ErrorKind);
        Assert.Empty(await scope.Db.B2BNegotiations.AsNoTracking().ToListAsync());

        var validStart = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 1.234m, null, null));
        Assert.True(validStart.Succeeded, validStart.Error);

        var invalidCounter = await scope.Negotiations.CounterOfferAsync(
            sellerUserId,
            validStart.Value,
            new CounterOfferInput([new B2BOfferLineInput(variantIds[0], 10)], 1.2345m, null, null));
        Assert.Equal(ResultErrorKind.Validation, invalidCounter.ErrorKind);

        var persisted = await scope.Db.B2BOfferRevisions.AsNoTracking()
            .Include(r => r.Lines)
            .SingleAsync(r => r.B2BNegotiationId == validStart.Value);
        Assert.Equal(1.234m, persisted.ProposedUnitPrice);
        Assert.Equal(12.340m, persisted.ProposedTotal);
        Assert.Equal(persisted.ProposedUnitPrice * persisted.TotalQuantity, persisted.ProposedTotal);
    }

    [SkippableFact]
    public async Task ApprovedMerchantWithAdminRole_CannotPerformAnyNegotiationAction()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);
        await scope.AddRoleAsync(sellerUserId, FaedRoles.Admin);

        var forbiddenStart = await scope.Negotiations.StartNegotiationAsync(sellerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        var forbiddenCounter = await scope.Negotiations.CounterOfferAsync(
            sellerUserId,
            start.Value,
            new CounterOfferInput([new B2BOfferLineInput(variantIds[0], 10)], 5m, null, null));
        var forbiddenAccept = await scope.Negotiations.AcceptAsync(sellerUserId, start.Value);
        var forbiddenReject = await scope.Negotiations.RejectAsync(sellerUserId, start.Value);
        var forbiddenCancel = await scope.Negotiations.CancelAsync(sellerUserId, start.Value);

        Assert.All(
            new Result[] { forbiddenCounter, forbiddenAccept, forbiddenReject, forbiddenCancel },
            result => Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind));
        Assert.Equal(ResultErrorKind.Forbidden, forbiddenStart.ErrorKind);
        Assert.Empty(await scope.Negotiations.GetMyNegotiationsAsync(sellerUserId, B2BNegotiationFilter.All));
        Assert.Null(await scope.Negotiations.GetNegotiationAsync(sellerUserId, start.Value));

        var persisted = await scope.Db.B2BNegotiations.AsNoTracking()
            .Include(n => n.Revisions)
            .SingleAsync(n => n.Id == start.Value);
        Assert.Equal(B2BNegotiationStatus.Open, persisted.Status);
        Assert.Single(persisted.Revisions);
    }

    [SkippableFact]
    public async Task RemoveVariant_WhenReferencedByOfferHistory_ReturnsValidationAndPreservesHistory()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new NegotiationScope(factory);
        var (sellerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (buyerUserId, _) = await scope.CreateApprovedMerchantAsync();
        var (slug, variantIds) = await scope.CreateLiveB2BListingAsync(sellerUserId, moq: 10);
        var listingId = await scope.Db.Listings.AsNoTracking()
            .Where(l => l.Slug == slug)
            .Select(l => l.Id)
            .SingleAsync();

        var start = await scope.Negotiations.StartNegotiationAsync(buyerUserId, new StartNegotiationInput(
            slug, [new B2BOfferLineInput(variantIds[0], 10)], 4m, null, null));
        Assert.True(start.Succeeded, start.Error);

        var remove = await scope.Listings.RemoveVariantAsync(sellerUserId, listingId, variantIds[0]);

        Assert.Equal(ResultErrorKind.Validation, remove.ErrorKind);
        Assert.Contains("Deactivate", remove.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(await scope.Db.ListingVariants.AsNoTracking().AnyAsync(v => v.Id == variantIds[0]));
        Assert.True(await scope.Db.B2BOfferLines.AsNoTracking().AnyAsync(l => l.ListingVariantId == variantIds[0]));

        var deactivate = await scope.Listings.SetVariantActiveAsync(sellerUserId, listingId, variantIds[0], false);
        Assert.True(deactivate.Succeeded, deactivate.Error);
        Assert.False(await scope.Db.ListingVariants.AsNoTracking()
            .Where(v => v.Id == variantIds[0])
            .Select(v => v.IsActive)
            .SingleAsync());
    }

    private sealed class NegotiationScope(FaedWebApplicationFactory factory) : IAsyncDisposable
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

        public async Task AddRoleAsync(string userId, string role)
        {
            var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);
            Assert.NotNull(user);
            Assert.True((await users.AddToRoleAsync(user!, role)).Succeeded);
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

        public async Task<(string Slug, IReadOnlyList<Guid> VariantIds)> CreateLiveB2BListingAsync(
            string merchantUserId, int moq, int initialQuantity = 20)
        {
            var referenceData = await Listings.GetReferenceDataAsync();
            var categoryId = referenceData.Categories[0].Id;
            var gradeId = referenceData.ConditionGrades.Single(g => g.Label.Contains("Grade A ")).Id;
            var reasonId = referenceData.DiscountReasons.Single(r => r.Label == "Overstock").Id;

            var details = new ListingDetailsInput(
                categoryId, null, gradeId, "Wholesale T-Shirts", "Overstock cotton tees.",
                null, 9.000m, 5.000m, moq, true, true, true, null, null, null, null, []);

            var create = await Listings.CreateAsync(merchantUserId, details);
            Assert.True(create.Succeeded, create.Error);
            var listingId = create.Value;

            Assert.True((await Listings.AddOptionAsync(merchantUserId, listingId, "Size")).Succeeded);
            var optionId = await Db.Set<ListingOption>().Where(o => o.ListingId == listingId).Select(o => o.Id).SingleAsync();

            var variantIds = new List<Guid>();
            foreach (var value in new[] { "M", "L" })
            {
                Assert.True((await Listings.AddOptionValueAsync(merchantUserId, listingId, optionId, value)).Succeeded);
                var valueId = await Db.Set<ListingOptionValue>()
                    .Where(v => v.ListingOptionId == optionId && v.Value == value).Select(v => v.Id).SingleAsync();
                Assert.True((await Listings.AddVariantAsync(
                    merchantUserId, listingId, new AddVariantInput($"TEE-{value}-{Guid.NewGuid():N}", [valueId], initialQuantity))).Succeeded);
            }

            Assert.True((await Listings.AddImageAsync(merchantUserId, listingId, new AddListingImageInput(
                ListingMediaType.Product, TestImages.MinimalPngStream(), "front.png", "image/png",
                TestImages.MinimalPng.Length, "Front view"))).Succeeded);
            Assert.True((await Listings.UpdateDetailsAsync(merchantUserId, listingId, details with { DiscountReasonIds = [reasonId] })).Succeeded);
            Assert.True((await Listings.SubmitForReviewAsync(merchantUserId, listingId)).Succeeded);
            var adminId = await CreateUserAsync(FaedRoles.Admin);
            Assert.True((await Moderation.ApproveAsync(adminId, listingId, null)).Succeeded);

            var ordered = await Db.ListingVariants.AsNoTracking()
                .Where(v => v.ListingId == listingId).OrderBy(v => v.Sku).Select(v => v.Id).ToListAsync();

            var slug = await Db.Listings.AsNoTracking().Where(l => l.Id == listingId).Select(l => l.Slug).SingleAsync();
            _listingIds.Add(listingId);
            return (slug, ordered);
        }

        public async ValueTask DisposeAsync()
        {
            if (_listingIds.Count > 0 || _merchantProfileIds.Count > 0)
            {
                await using var cleanupDb = new ApplicationDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseSqlServer(Db.Database.GetConnectionString()
                            ?? throw new InvalidOperationException("The test DbContext has no connection string."))
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

                if (merchantIds.Count > 0)
                {
                    cleanupDb.MerchantProfiles.RemoveRange(
                        await cleanupDb.MerchantProfiles.Where(p => merchantIds.Contains(p.Id)).ToListAsync());
                    await cleanupDb.SaveChangesAsync();
                }
            }

            _scope.Dispose();
        }
    }
}
