using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Entities;
using Faed.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// Catalog reference-data seeding against real SQL Server (tasks/TASK-003-CATALOG.md
/// exit criteria, docs/09-TEST-STRATEGY.md §2). The hosted factory applies migrations and
/// runs <see cref="CatalogDataSeeder"/> at startup.
/// </summary>
[Collection(FaedWebCollection.Name)]
public sealed class CatalogSeedTests(FaedWebApplicationFactory factory)
{
    [SkippableFact]
    public async Task Startup_SeedsLaunchTaxonomy_GradesAndAllEightReasons()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var grades = await db.ConditionGrades.AsNoTracking()
            .OrderBy(g => g.SortOrder).Select(g => g.Code).ToListAsync();
        Assert.Equal(new[] { "A", "B", "C", "D" }, grades);

        Assert.Equal(8, await db.DiscountReasons.AsNoTracking().CountAsync());

        var root = await db.Categories.AsNoTracking()
            .SingleAsync(c => c.Slug == CatalogDataSeeder.RootCategorySlug);
        Assert.Null(root.ParentCategoryId);

        var launch = await db.Categories.AsNoTracking()
            .Where(c => c.ParentCategoryId == root.Id)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Name)
            .ToListAsync();
        Assert.Equal(new[] { "Clothing", "Shoes", "Bags & Accessories" }, launch);
    }

    [SkippableFact]
    public async Task ConditionGrades_DoNotIncludeGradeE()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await db.ConditionGrades.AsNoTracking().AnyAsync(g => g.Code == "E"));
    }

    [SkippableFact]
    public async Task Seed_RunAgain_AddsNoDuplicateRows()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        async Task<(int Categories, int Grades, int Reasons)> CountAsync()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (
                await db.Categories.CountAsync(),
                await db.ConditionGrades.CountAsync(),
                await db.DiscountReasons.CountAsync());
        }

        var before = await CountAsync();
        await CatalogDataSeeder.SeedAsync(factory.Services);
        await CatalogDataSeeder.SeedAsync(factory.Services);
        var after = await CountAsync();

        Assert.Equal(before, after);
    }

    [SkippableFact]
    public async Task Seed_IsIdempotent_WhenAnExistingSlugDiffersOnlyByCasing()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var root = await db.Categories.SingleAsync(c => c.Slug == CatalogDataSeeder.RootCategorySlug);
        var clothing = await db.Categories.SingleAsync(c => c.Slug == "clothing");
        var sortOrder = clothing.SortOrder;

        // Simulate a later admin edit that stored the slug with different casing. SQL Server's
        // default collation is case-insensitive, so the seeder must recognise this as the
        // same key and not attempt a duplicate insert.
        db.Categories.Remove(clothing);
        await db.SaveChangesAsync();
        db.Categories.Add(new Category("Clothing", "CLOTHING", root.Id, sortOrder));
        await db.SaveChangesAsync();

        try
        {
            await CatalogDataSeeder.SeedAsync(factory.Services);

            var children = await db.Categories.AsNoTracking().CountAsync(c => c.ParentCategoryId == root.Id);
            Assert.Equal(3, children);
        }
        finally
        {
            var current = await db.Categories.SingleAsync(c => c.Slug == "clothing");
            db.Categories.Remove(current);
            await db.SaveChangesAsync();
            db.Categories.Add(new Category("Clothing", "clothing", root.Id, sortOrder));
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task SecondRootCategory_CanBeAddedByDataAlone_WithNoSchemaChange()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // A future sector is a plain row under a null parent — no migration, no new entity
        // (AGENTS.md §3, docs/14-FUTURE-EXPANSION.md).
        var secondRoot = new Category("Home Supplies (test)", "home-supplies-test-root", parentCategoryId: null, sortOrder: 50);
        db.Categories.Add(secondRoot);
        await db.SaveChangesAsync();

        try
        {
            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var rootSlugs = await verifyDb.Categories.AsNoTracking()
                .Where(c => c.ParentCategoryId == null)
                .Select(c => c.Slug)
                .ToListAsync();

            Assert.Contains(CatalogDataSeeder.RootCategorySlug, rootSlugs);
            Assert.Contains("home-supplies-test-root", rootSlugs);
        }
        finally
        {
            db.Categories.Remove(secondRoot);
            await db.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task CategorySlug_IsUniqueAtTheDatabase()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Categories.Add(new Category("Clothing Duplicate", "clothing", null, 99));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task BrandSlug_IsUniqueAtTheDatabase()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Brands.Add(new Brand("Acme One", "acme-shared-slug"));
        db.Brands.Add(new Brand("Acme Two", "acme-shared-slug"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
