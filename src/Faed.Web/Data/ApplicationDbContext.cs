using Faed.Web.Services.Abstractions;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faed.Web.Data;

/// <summary>
/// The single application DbContext (AGENTS.md §5, docs/06-ARCHITECTURE.md §5).
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

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Entity configurations are applied from this assembly as aggregates are introduced.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
