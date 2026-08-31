using Faed.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Faed.Infrastructure.Persistence;

/// <summary>
/// The single application DbContext (AGENTS.md §5, docs/06-ARCHITECTURE.md §5).
/// Identity shares this context. Marketplace aggregates are added in later phases.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Entity configurations are applied from this assembly as aggregates are introduced.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
