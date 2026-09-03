using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
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
    /// <summary>
    /// The Identity user table. Exposed read-mostly for admin screens that must show who
    /// performed an audited action (docs/08-SECURITY-AND-PRIVACY.md §13); account creation
    /// and role changes still go through ASP.NET Core Identity, never through this set.
    /// </summary>
    DbSet<ApplicationUser> Users { get; }

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

    /// <summary>Post-transaction disputes against exactly one order or deal (docs/03-BUSINESS-RULES.md §14, TASK-009).</summary>
    DbSet<Dispute> Disputes { get; }

    /// <summary>Private evidence files attached to a dispute (docs/08-SECURITY-AND-PRIVACY.md §3-4).</summary>
    DbSet<DisputeEvidence> DisputeEvidence { get; }

    /// <summary>Merchant reviews left after a completed transaction (docs/03-BUSINESS-RULES.md §13, TASK-009).</summary>
    DbSet<Review> Reviews { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit database transaction so a use case that must persist several
    /// changes together (for example a verification decision and its Identity role grant)
    /// commits atomically or not at all (AGENTS.md §7).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
