using Faed.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faed.Application.Abstractions;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
