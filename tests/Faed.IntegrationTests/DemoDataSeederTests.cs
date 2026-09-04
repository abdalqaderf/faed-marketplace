using Faed.Web.Data;
using Faed.Web.Data.Seed;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faed.IntegrationTests;

/// <summary>
/// The deterministic demo/field-validation data set (docs/12-SEED-DATA.md,
/// tasks/TASK-011-HARDENING-AND-DEMO.md). The seeder drives every scenario through the real
/// application services against real SQL Server, so this also exercises the full
/// verification → listing → order → negotiation → deal → dispute → review path end to end,
/// plus — for finding 1 of the final review — idempotency and recovery after an interrupted
/// run.
///
/// Runs against its own disposable catalog (<see cref="DemoSeedWebApplicationFactory"/>).
/// </summary>
[Collection(DemoSeedWebCollection.Name)]
public sealed class DemoDataSeederTests(DemoSeedWebApplicationFactory factory)
{
    private const string Password = "Demo-Passw0rd!";

    private async Task<bool> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await DemoDataSeeder.SeedCoreAsync(scope.ServiceProvider, Password)
            .WaitAsync(TimeSpan.FromMinutes(10));
    }

    [SkippableFact]
    public async Task Seed_BuildsTheDocumentedDemoDataSet_IsIdempotent_AndResumesAfterAnInterruption()
    {
        Skip.IfNot(factory.DatabaseReady, "SQL Server not reachable.");

        Assert.True(await SeedAsync(), "the first seed on a clean database must run");
        await AssertFullDataSetAsync();

        // Idempotent: a second run does nothing and changes nothing.
        var before = await SnapshotAsync();
        Assert.False(await SeedAsync());
        Assert.Equal(before, await SnapshotAsync());

        // Recovery after an interrupted run: delete the completion marker (the buyer's
        // review), then re-run. The seeder finds demo accounts but no marker, purges the
        // partial data in foreign-key-safe order, and rebuilds the whole set (with fresh ids).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Reviews.RemoveRange(await db.Reviews.ToListAsync());
            await db.SaveChangesAsync();
        }

        Assert.True(await SeedAsync(), "with the completion marker gone the seed must resume");
        await AssertFullDataSetAsync();
    }

    private async Task AssertFullDataSetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var email in new[]
        {
            DemoDataSeeder.AdminEmail, DemoDataSeeder.MerchantAEmail, DemoDataSeeder.MerchantBEmail,
            DemoDataSeeder.PendingMerchantEmail, DemoDataSeeder.BuyerAEmail, DemoDataSeeder.BuyerBEmail,
        })
        {
            Assert.NotNull(await users.FindByEmailAsync(email));
        }

        var merchantA = await ApprovedMerchantProfileIdAsync(db, users, DemoDataSeeder.MerchantAEmail);
        await ApprovedMerchantProfileIdAsync(db, users, DemoDataSeeder.MerchantBEmail);

        var pendingUserId = (await users.FindByEmailAsync(DemoDataSeeder.PendingMerchantEmail))!.Id;
        var pending = await db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.UserId == pendingUserId);
        Assert.Equal(MerchantVerificationStatus.PendingReview, pending.VerificationStatus);

        var listings = await db.Listings.AsNoTracking().ToListAsync();
        Assert.Equal(12, listings.Count);
        Assert.Equal(11, listings.Count(l => l.Status == ListingStatus.Live));
        Assert.Equal(1, listings.Count(l => l.Status == ListingStatus.SoldOut));
        Assert.Contains(listings, l => l.AllowB2B);
        Assert.Contains(listings, l => !l.AllowB2B);
        Assert.Contains(await db.ListingMedia.AsNoTracking().ToListAsync(), m => m.MediaType == ListingMediaType.Defect);

        var brands = await db.Brands.AsNoTracking().Select(b => b.Name).ToListAsync();
        Assert.Contains("Nova Basics", brands);
        Assert.Contains("TrailHead", brands);

        var orders = await db.Orders.AsNoTracking().ToListAsync();
        Assert.Contains(orders, o => o.Status == OrderStatus.Confirmed);       // one active B2C order
        Assert.Contains(orders, o => o.Status == OrderStatus.Completed);       // one completed B2C order
        Assert.Contains(orders, o => o.Status == OrderStatus.OutForDelivery);  // one dispatched delivery order
        Assert.Contains(orders, o => o.FulfillmentType == OrderFulfillmentType.MerchantDelivery);

        Assert.Contains(await db.InventoryAdjustments.AsNoTracking().ToListAsync(),
            a => a.AdjustmentType == InventoryAdjustmentType.StockFound);

        var negotiations = await db.B2BNegotiations.AsNoTracking().ToListAsync();
        Assert.Contains(negotiations, n => n.Status == B2BNegotiationStatus.Open);
        Assert.Contains(negotiations, n => n.Status == B2BNegotiationStatus.Accepted);

        var revisionCounts = await db.B2BOfferRevisions.AsNoTracking()
            .GroupBy(r => r.B2BNegotiationId).Select(g => g.Count()).ToListAsync();
        Assert.Contains(revisionCounts, count => count >= 2);   // a counter-offer chain

        var deal = Assert.Single(await db.B2BDeals.AsNoTracking().ToListAsync());
        Assert.Equal(B2BDealStatus.Completed, deal.Status);

        Assert.Contains(await db.Disputes.AsNoTracking().ToListAsync(), d => d.B2BDealId == deal.Id);
        Assert.Single(await db.Reviews.AsNoTracking()
            .Where(r => r.ReviewedMerchantProfileId == merchantA && r.Rating == 5).ToListAsync());
    }

    private async Task<(int Listings, int Orders, int Negotiations, int Deals, int Users)> SnapshotAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (
            await db.Listings.CountAsync(),
            await db.Orders.CountAsync(),
            await db.B2BNegotiations.CountAsync(),
            await db.B2BDeals.CountAsync(),
            await db.Users.CountAsync());
    }

    private static async Task<Guid> ApprovedMerchantProfileIdAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> users, string email)
    {
        var userId = (await users.FindByEmailAsync(email))!.Id;
        var profile = await db.MerchantProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
        Assert.Equal(MerchantVerificationStatus.Approved, profile.VerificationStatus);
        return profile.Id;
    }
}
