using Faed.Web.Services.Abstractions;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faed.Web.Data;

/// <summary>
/// The single application DbContext.
/// Identity shares this context. Marketplace aggregates are added in later phases.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<MerchantProfile> MerchantProfiles => Set<MerchantProfile>();

    public DbSet<MerchantVerificationDocument> MerchantVerificationDocuments => Set<MerchantVerificationDocument>();

    public DbSet<AdminActionLog> AdminActionLogs => Set<AdminActionLog>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ConditionGrade> ConditionGrades => Set<ConditionGrade>();

    public DbSet<DiscountReason> DiscountReasons => Set<DiscountReason>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<ListingVariant> ListingVariants => Set<ListingVariant>();

    public DbSet<ListingMedia> ListingMedia => Set<ListingMedia>();

    public DbSet<ListingReferencePriceEvidence> ListingReferencePriceEvidence => Set<ListingReferencePriceEvidence>();

    public DbSet<ListingModeration> ListingModerations => Set<ListingModeration>();

    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();

    public DbSet<MerchantLocation> MerchantLocations => Set<MerchantLocation>();

    public DbSet<MerchantDeliveryZone> MerchantDeliveryZones => Set<MerchantDeliveryZone>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<B2BNegotiation> B2BNegotiations => Set<B2BNegotiation>();

    public DbSet<B2BOfferRevision> B2BOfferRevisions => Set<B2BOfferRevision>();

    public DbSet<B2BOfferLine> B2BOfferLines => Set<B2BOfferLine>();

    public DbSet<B2BDeal> B2BDeals => Set<B2BDeal>();

    public DbSet<B2BDealLine> B2BDealLines => Set<B2BDealLine>();

    public DbSet<Dispute> Disputes => Set<Dispute>();

    public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>();

    public DbSet<Review> Reviews => Set<Review>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Entity configurations are applied from this assembly as aggregates are introduced.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
