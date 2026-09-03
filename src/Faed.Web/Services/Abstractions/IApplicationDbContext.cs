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

    DbSet<MerchantLocation> MerchantLocations { get; }

    DbSet<MerchantDeliveryZone> MerchantDeliveryZones { get; }

    /// <summary>B2C orders (AGENTS.md Rule D). One buyer, one selling merchant, one or more variant lines.</summary>
    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    /// <summary>B2B merchant-to-merchant negotiations (AGENTS.md Rule C, docs/adr/0004). Not a fulfillment record.</summary>
    DbSet<B2BNegotiation> B2BNegotiations { get; }

    /// <summary>Immutable offer/counter-offer revisions (docs/17-DATA-INVARIANTS.md "Previous revisions are immutable").</summary>
    DbSet<B2BOfferRevision> B2BOfferRevisions { get; }

    DbSet<B2BOfferLine> B2BOfferLines { get; }

    /// <summary>Accepted B2B deals: the fulfillment record with its own reservation and stock (docs/adr/0004, TASK-008).</summary>
    DbSet<B2BDeal> B2BDeals { get; }

    DbSet<B2BDealLine> B2BDealLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit database transaction so a use case that must persist several
    /// changes together (for example a verification decision and its Identity role grant)
    /// commits atomically or not at all (AGENTS.md §7).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
