using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.IntegrationTests;

/// <summary>
/// Admin catalog management against real SQL Server (tasks/TASK-010-ANALYTICS-AND-ADMIN.md
/// "catalog management"; docs/16-PERMISSIONS-MATRIX.md "Manage catalog reference data —
/// Admin"). Every write re-checks the admin role and is audited
/// (docs/08-SECURITY-AND-PRIVACY.md §2, §13).
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class AdminCatalogServiceTests(FaedWebApplicationFactory factory)
{
    private static IAdminCatalogService Catalog(TrustScope scope) =>
        scope.Services.GetRequiredService<IAdminCatalogService>();

    [SkippableFact]
    public async Task NonAdmin_CannotChangeCatalogData_EvenAtTheServiceLayer()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var plainUserId = await scope.CreateUserAsync();

        var create = await Catalog(scope).CreateBrandAsync(plainUserId, "Rogue Brand");
        Assert.Equal(ResultErrorKind.Forbidden, create.ErrorKind);
        Assert.False(await scope.Db.Brands.AsNoTracking().AnyAsync(b => b.Name == "Rogue Brand"));
    }

    [SkippableFact]
    public async Task Admin_CanManageBrands_AndEachChangeIsAudited()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);
        var uniqueName = $"Test Brand {Guid.NewGuid():N}";

        var created = await Catalog(scope).CreateBrandAsync(adminUserId, uniqueName);
        Assert.True(created.Succeeded, created.Error);

        var renamed = $"{uniqueName} Renamed";
        Assert.True((await Catalog(scope).RenameBrandAsync(adminUserId, created.Value, renamed)).Succeeded);
        Assert.True((await Catalog(scope).SetBrandActiveAsync(adminUserId, created.Value, false)).Succeeded);

        var brand = await scope.Db.Brands.AsNoTracking().SingleAsync(b => b.Id == created.Value);
        Assert.Equal(renamed, brand.Name);
        Assert.False(brand.IsActive);

        var auditRows = await scope.Db.AdminActionLogs.AsNoTracking()
            .Where(l => l.TargetType == nameof(Faed.Web.Models.Entities.Brand)
                && l.TargetId == created.Value.ToString())
            .Select(l => l.ActionType)
            .ToListAsync();
        Assert.Contains(AdminActionType.CatalogItemCreated, auditRows);
        Assert.Contains(AdminActionType.CatalogItemUpdated, auditRows);
        Assert.Contains(AdminActionType.CatalogItemAvailabilityChanged, auditRows);
    }

    [SkippableFact]
    public async Task DuplicateDiscountReasonCode_IsRejected()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);

        // "Overstock" is a seeded reason code.
        var dup = await Catalog(scope).CreateDiscountReasonAsync(adminUserId, "Overstock", "Overstock again", null);
        Assert.Equal(ResultErrorKind.Conflict, dup.ErrorKind);
    }

    [SkippableFact]
    public async Task Overview_ListsSeededReferenceDataWithListingCounts()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);

        var overview = await Catalog(scope).GetOverviewAsync();

        Assert.Contains(overview.ConditionGrades, g => g.Code == "A");
        Assert.Equal(4, overview.ConditionGrades.Count);
        Assert.Contains(overview.DiscountReasons, r => r.Code == "Overstock");
        Assert.Contains(overview.Categories, c => c.Slug == "fashion-overstock" && c.Depth == 0);
        Assert.Contains(overview.Categories, c => c.Slug == "clothing" && c.Depth == 1);
    }

    [SkippableFact]
    public async Task FashionMvp_CatalogAndMerchantListingEligibilityExcludeOtherSectorCategories()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);
        var (merchantUserId, _) = await scope.CreateApprovedMerchantAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var futureRoot = new Category("Future Home", $"future-home-{suffix}", null, 0);
        var futureChild = new Category("Kitchenware", $"kitchenware-{suffix}", futureRoot.Id, 1);
        Guid? createdLaunchCategoryId = null;
        scope.Db.Categories.AddRange(futureRoot, futureChild);
        await scope.Db.SaveChangesAsync();

        try
        {
            var references = await scope.Listings.GetReferenceDataAsync();
            Assert.DoesNotContain(references.Categories, c => c.Id == futureChild.Id);

            var overview = await Catalog(scope).GetOverviewAsync();
            Assert.DoesNotContain(overview.Categories, c => c.Id == futureRoot.Id || c.Id == futureChild.Id);
            var launchRootId = overview.Categories.Single(c => c.Slug == "fashion-overstock").Id;

            var addInsideLaunch = await Catalog(scope).CreateCategoryAsync(
                adminUserId, launchRootId, $"Launch Child {suffix}", 99);
            Assert.True(addInsideLaunch.Succeeded, addInsideLaunch.Error);
            createdLaunchCategoryId = addInsideLaunch.Value;
            Assert.Contains(
                (await scope.Listings.GetReferenceDataAsync()).Categories,
                c => c.Id == createdLaunchCategoryId);

            var addRoot = await Catalog(scope).CreateCategoryAsync(adminUserId, null, "Another Sector", 0);
            Assert.Equal(ResultErrorKind.Validation, addRoot.ErrorKind);
            var addOutsideLaunch = await Catalog(scope).CreateCategoryAsync(
                adminUserId, futureRoot.Id, "Outside Launch Child", 0);
            Assert.Equal(ResultErrorKind.Validation, addOutsideLaunch.ErrorKind);

            var gradeId = references.ConditionGrades[0].Id;
            var reasonId = references.DiscountReasons[0].Id;
            var craftedListing = await scope.Listings.CreateAsync(merchantUserId, new ListingDetailsInput(
                futureChild.Id,
                null,
                gradeId,
                "Out-of-sector listing",
                "This category must remain unavailable during the Fashion MVP.",
                null,
                10m,
                null,
                null,
                true,
                false,
                false,
                null,
                null,
                null,
                null,
                [reasonId]));
            Assert.Equal(ResultErrorKind.Validation, craftedListing.ErrorKind);
        }
        finally
        {
            scope.Db.ChangeTracker.Clear();
            if (createdLaunchCategoryId is { } launchCategoryId)
            {
                await scope.Db.AdminActionLogs
                    .Where(l => l.TargetType == nameof(Category) && l.TargetId == launchCategoryId.ToString())
                    .ExecuteDeleteAsync();
                await scope.Db.Categories.Where(c => c.Id == launchCategoryId).ExecuteDeleteAsync();
            }
            await scope.Db.Categories.Where(c => c.Id == futureChild.Id).ExecuteDeleteAsync();
            await scope.Db.Categories.Where(c => c.Id == futureRoot.Id).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ConcurrentBrandSlugCollision_ReturnsControlledConflict()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);
        var name = $"Concurrent Brand {Guid.NewGuid():N}";
        var gated = NewGatedCatalog(scope, async cancellationToken =>
        {
            var winner = await Catalog(scope).CreateBrandAsync(adminUserId, name, cancellationToken);
            Assert.True(winner.Succeeded, winner.Error);
        });

        var loser = await gated.CreateBrandAsync(adminUserId, name);

        Assert.Equal(ResultErrorKind.Conflict, loser.ErrorKind);
        var brands = await scope.Db.Brands.Where(b => b.Name == name).ToListAsync();
        Assert.Single(brands);
        var brandId = brands[0].Id;
        Assert.Single(await scope.Db.AdminActionLogs
            .Where(l => l.TargetType == nameof(Brand) && l.TargetId == brandId.ToString())
            .ToListAsync());

        scope.Db.AdminActionLogs.RemoveRange(scope.Db.AdminActionLogs.Where(
            l => l.TargetType == nameof(Brand) && l.TargetId == brandId.ToString()));
        scope.Db.Brands.Remove(brands[0]);
        await scope.Db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task ConcurrentDiscountReasonCodeCollision_ReturnsControlledConflict()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        await using var scope = new TrustScope(factory);
        var adminUserId = await scope.CreateUserAsync(FaedRoles.Admin);
        var code = $"Race{Guid.NewGuid():N}";
        var gated = NewGatedCatalog(scope, async cancellationToken =>
        {
            var winner = await Catalog(scope).CreateDiscountReasonAsync(
                adminUserId, code, "Concurrent reason", null, cancellationToken);
            Assert.True(winner.Succeeded, winner.Error);
        });

        var loser = await gated.CreateDiscountReasonAsync(adminUserId, code, "Concurrent reason", null);

        Assert.Equal(ResultErrorKind.Conflict, loser.ErrorKind);
        var reasons = await scope.Db.DiscountReasons.Where(r => r.Code == code).ToListAsync();
        Assert.Single(reasons);
        var reasonId = reasons[0].Id;
        Assert.Single(await scope.Db.AdminActionLogs
            .Where(l => l.TargetType == nameof(DiscountReason) && l.TargetId == reasonId.ToString())
            .ToListAsync());

        scope.Db.AdminActionLogs.RemoveRange(scope.Db.AdminActionLogs.Where(
            l => l.TargetType == nameof(DiscountReason) && l.TargetId == reasonId.ToString()));
        scope.Db.DiscountReasons.Remove(reasons[0]);
        await scope.Db.SaveChangesAsync();
    }

    private static AdminCatalogService NewGatedCatalog(
        TrustScope scope, Func<CancellationToken, Task> beforeFirstSave) => new(
        new GatedApplicationDbContext(scope.CreateDbContext(), beforeFirstSave),
        scope.Services.GetRequiredService<IUserRoleService>(),
        scope.Services.GetRequiredService<IClock>(),
        scope.Services.GetRequiredService<ILogger<AdminCatalogService>>());
}
