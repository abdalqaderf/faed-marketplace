using Faed.Web.Services.Abstractions;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Entity configurations are applied from this assembly as aggregates are introduced.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
