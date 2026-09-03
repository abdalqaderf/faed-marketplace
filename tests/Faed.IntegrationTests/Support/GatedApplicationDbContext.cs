using Faed.Web.Data;
using Faed.Web.Models.Entities;
using Faed.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faed.IntegrationTests.Support;

/// <summary>
/// An <see cref="IApplicationDbContext"/> that runs a caller-supplied hook exactly once,
/// immediately before the first real <c>SaveChangesAsync</c>. It lets a test deterministically
/// interleave two use-case executions: run one service call up to the point it is about to
/// persist, let a competing call complete and commit inside the hook, then release the first
/// call's write so it hits the moved concurrency token. This is the same idea as
/// <c>MerchantVerificationServiceTests.BeforeFirstSaveApplicationDbContext</c>, shared here.
/// </summary>
public sealed class GatedApplicationDbContext(
    ApplicationDbContext inner,
    Func<CancellationToken, Task> beforeFirstSave) : IApplicationDbContext
{
    private int _saveStarted;

    public DbSet<MerchantProfile> MerchantProfiles => inner.MerchantProfiles;

    public DbSet<MerchantVerificationDocument> MerchantVerificationDocuments => inner.MerchantVerificationDocuments;

    public DbSet<AdminActionLog> AdminActionLogs => inner.AdminActionLogs;

    public DbSet<Category> Categories => inner.Categories;

    public DbSet<ConditionGrade> ConditionGrades => inner.ConditionGrades;

    public DbSet<DiscountReason> DiscountReasons => inner.DiscountReasons;

    public DbSet<Brand> Brands => inner.Brands;

    public DbSet<Listing> Listings => inner.Listings;

    public DbSet<ListingVariant> ListingVariants => inner.ListingVariants;

    public DbSet<ListingMedia> ListingMedia => inner.ListingMedia;

    public DbSet<ListingReferencePriceEvidence> ListingReferencePriceEvidence => inner.ListingReferencePriceEvidence;

    public DbSet<ListingModeration> ListingModerations => inner.ListingModerations;

    public DbSet<InventoryAdjustment> InventoryAdjustments => inner.InventoryAdjustments;

    public DbSet<MerchantLocation> MerchantLocations => inner.MerchantLocations;

    public DbSet<MerchantDeliveryZone> MerchantDeliveryZones => inner.MerchantDeliveryZones;

    public DbSet<Order> Orders => inner.Orders;

    public DbSet<OrderItem> OrderItems => inner.OrderItems;

    public DbSet<B2BNegotiation> B2BNegotiations => inner.B2BNegotiations;

    public DbSet<B2BOfferRevision> B2BOfferRevisions => inner.B2BOfferRevisions;

    public DbSet<B2BOfferLine> B2BOfferLines => inner.B2BOfferLines;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _saveStarted, 1) == 0)
        {
            await beforeFirstSave(cancellationToken);
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.Database.BeginTransactionAsync(cancellationToken);
}
