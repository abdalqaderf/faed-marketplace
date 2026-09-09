using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Faed.Web.Services.Abstractions;

/// <summary>
/// The subset of the single application <c>DbContext</c> that application services use.
/// This is a purposeful seam, not a generic repository:
/// services still write LINQ queries directly against these sets.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// The Identity user table. Exposed read-mostly for admin screens that must show who
    /// performed an audited action; account creation
    /// and role changes still go through ASP.NET Core Identity, never through this set.
    /// </summary>
    DbSet<ApplicationUser> Users { get; }

    DbSet<MerchantProfile> MerchantProfiles { get; }

    DbSet<MerchantVerificationDocument> MerchantVerificationDocuments { get; }

    DbSet<Category> Categories { get; }

    DbSet<ConditionGrade> ConditionGrades { get; }

    DbSet<DiscountReason> DiscountReasons { get; }

    DbSet<Listing> Listings { get; }

    /// <summary>The authoritative inventory records.</summary>
    DbSet<ListingVariant> ListingVariants { get; }

    DbSet<ListingMedia> ListingMedia { get; }

    DbSet<ListingReferencePriceEvidence> ListingReferencePriceEvidence { get; }

    DbSet<ListingModeration> ListingModerations { get; }

    DbSet<MerchantLocation> MerchantLocations { get; }

    /// <summary>B2C orders. One buyer, one selling merchant, one or more variant lines.</summary>
    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    /// <summary>Merchant reviews left after a completed transaction.</summary>
    DbSet<Review> Reviews { get; }

    /// <summary>Seeded, admin-editable monthly plans (Basic/Standard/Pro).</summary>
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }

    /// <summary>One row per merchant: the subscription state that gates publishing.</summary>
    DbSet<MerchantSubscription> MerchantSubscriptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit database transaction so a use case that must persist several
    /// changes together (for example a verification decision and its Identity role grant)
    /// commits atomically or not at all.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
