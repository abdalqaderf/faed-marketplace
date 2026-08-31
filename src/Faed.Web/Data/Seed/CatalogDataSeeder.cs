using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Data.Seed;

/// <summary>
/// Idempotent seeding of the fixed Faed catalog reference data: condition grades A–D, the
/// eight PRD-approved discount reasons, and the launch taxonomy (<c>Fashion Overstock</c> →
/// Clothing, Shoes, Bags &amp; Accessories). See tasks/TASK-003-CATALOG.md and
/// docs/12-SEED-DATA.md.
///
/// Runs in every environment at startup, after <see cref="IdentityDataSeeder"/>. The schema
/// must already exist (apply migrations first — the app does not migrate on startup). Each
/// row is matched on its natural key (grade / reason <c>Code</c>, category <c>Slug</c>) and
/// only inserted when missing, so re-running never duplicates and never overwrites a later
/// admin edit (full catalog management is TASK-010). Key comparison is
/// case-insensitive to match SQL Server's default case-insensitive collation, so a
/// differently-cased existing row (for example from a later admin edit) is still treated as
/// present rather than causing a duplicate-key insert on startup. Deeper taxonomy is
/// deferred (docs/13-OPEN-QUESTIONS.md item 4); no brands are seeded (items 5–6).
/// </summary>
public static class CatalogDataSeeder
{
    public const string RootCategorySlug = "fashion-overstock";

    // docs/01-PRD.md §6. Grades A–D only — no used-goods Grade E in the Fashion MVP.
    private static readonly (string Code, string Name, string Description, int SortOrder)[] Grades =
    [
        ("A", "New / Complete",
            "New, unused and complete, with normal packaging and tags where expected.", 1),
        ("B", "New / Packaging Imperfection",
            "New and unused, but the packaging, tag or box is damaged or missing.", 2),
        ("C", "Opened or Returned / Unused",
            "Opened, inspected or customer-returned, but not used or worn and still physically sound.", 3),
        ("D", "Display / Cosmetic Imperfection",
            "Display item or minor cosmetic imperfection that does not prevent normal use and is clearly disclosed.", 4),
    ];

    // docs/01-PRD.md §7 — all eight approved reasons, including OtherApprovedReason.
    private static readonly (string Code, string Name)[] Reasons =
    [
        ("Overstock", "Overstock"),
        ("PastSeason", "Past Season"),
        ("CustomerReturn", "Customer Return"),
        ("DisplayItem", "Display Item"),
        ("PackagingDamage", "Packaging Damage"),
        ("CosmeticDefect", "Cosmetic Defect"),
        ("MissingNonEssentialItem", "Missing Non-Essential Item"),
        ("OtherApprovedReason", "Other Approved Reason"),
    ];

    // AGENTS.md §3 launch categories. Lower-level taxonomy is deferred (TASK-003).
    private static readonly (string Slug, string Name, int SortOrder)[] LaunchCategories =
    [
        ("clothing", "Clothing", 1),
        ("shoes", "Shoes", 2),
        ("bags-accessories", "Bags & Accessories", 3),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(CatalogDataSeeder).FullName!);

        var added = 0;
        added += await SeedConditionGradesAsync(db, cancellationToken);
        added += await SeedDiscountReasonsAsync(db, cancellationToken);
        added += await SeedLaunchTaxonomyAsync(db, cancellationToken);

        if (added == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} catalog reference row(s).", added);
    }

    private static async Task<int> SeedConditionGradesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existing = (await db.ConditionGrades.Select(g => g.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name, description, sortOrder) in Grades)
        {
            if (existing.Add(code))
            {
                db.ConditionGrades.Add(new ConditionGrade(code, name, description, sortOrder));
                added++;
            }
        }

        return added;
    }

    private static async Task<int> SeedDiscountReasonsAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existing = (await db.DiscountReasons.Select(r => r.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name) in Reasons)
        {
            if (existing.Add(code))
            {
                db.DiscountReasons.Add(new DiscountReason(code, name));
                added++;
            }
        }

        return added;
    }

    private static async Task<int> SeedLaunchTaxonomyAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existingSlugs = (await db.Categories.Select(c => c.Slug).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        var root = await db.Categories.FirstOrDefaultAsync(c => c.Slug == RootCategorySlug, cancellationToken);
        if (root is null)
        {
            root = new Category("Fashion Overstock", RootCategorySlug, parentCategoryId: null, sortOrder: 0);
            db.Categories.Add(root);
            existingSlugs.Add(RootCategorySlug);
            added++;
        }

        foreach (var (slug, name, sortOrder) in LaunchCategories)
        {
            if (existingSlugs.Add(slug))
            {
                db.Categories.Add(new Category(name, slug, root.Id, sortOrder));
                added++;
            }
        }

        return added;
    }
}
