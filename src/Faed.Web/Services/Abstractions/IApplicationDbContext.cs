using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faed.Web.Services.Abstractions;

/// <summary>
/// The subset of the single application <c>DbContext</c> that application services use.
/// This is a purposeful seam, not a generic repository (docs/06-ARCHITECTURE.md §4):
/// services still write LINQ queries directly against these sets.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<MerchantProfile> MerchantProfiles { get; }

    DbSet<MerchantVerificationDocument> MerchantVerificationDocuments { get; }

    DbSet<AdminActionLog> AdminActionLogs { get; }

    DbSet<Category> Categories { get; }

    DbSet<ConditionGrade> ConditionGrades { get; }

    DbSet<DiscountReason> DiscountReasons { get; }

    DbSet<Brand> Brands { get; }

    DbSet<Listing> Listings { get; }

    /// <summary>The authoritative inventory records (AGENTS.md Rule A, docs/adr/0002).</summary>
    DbSet<ListingVariant> ListingVariants { get; }

    DbSet<ListingMedia> ListingMedia { get; }

    DbSet<ListingReferencePriceEvidence> ListingReferencePriceEvidence { get; }

    DbSet<ListingModeration> ListingModerations { get; }

    DbSet<InventoryAdjustment> InventoryAdjustments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit database transaction so a use case that must persist several
    /// changes together (for example a verification decision and its Identity role grant)
    /// commits atomically or not at all (AGENTS.md §7).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
