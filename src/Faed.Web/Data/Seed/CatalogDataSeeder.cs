using Faed.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Data.Seed;

/// <summary>
/// Idempotent seeding of the fixed Faed catalog reference data: condition grades A–D, the
/// eight approved discount reasons, and the launch taxonomy (<c>Open-Box &amp; Ex-Display</c> →
/// Small Kitchen Appliances, Home &amp; Cleaning Appliances, Power Tools &amp; Workshop).
/// Runs in every environment at startup, after <see cref="IdentityDataSeeder"/>. The schema
/// must already exist (apply migrations first — the app does not migrate on startup). Each
/// row is matched on its natural key (grade / reason <c>Code</c>, category <c>Slug</c>) and
/// only inserted when missing, so re-running never duplicates and never overwrites a later
/// admin edit. Key comparison is
/// case-insensitive to match SQL Server's default case-insensitive collation, so a
/// differently-cased existing row (for example from a later admin edit) is still treated as
/// present rather than causing a duplicate-key insert on startup. Deeper taxonomy is
/// deferred; no brands are seeded (items 5–6).
/// </summary>
public static class CatalogDataSeeder
{
    public const string RootCategorySlug = "open-box-ex-display";

    // Grades A–D only — no used-goods Grade E in the appliances MVP.
    private static readonly (string Code, string Name, string Description, int SortOrder)[] Grades =
    [
        ("A", "Sealed",
            "New and unopened in the original box.", 1),
        ("B", "Box Opened or Damaged",
            "The appliance is new and unused, but the packaging is opened, dented or torn.", 2),
        ("C", "Customer Return",
            "Opened and inspected by a previous customer, but never used.", 3),
        ("D", "Ex-Display",
            "A showroom unit with light scratches or marks from display use, clearly disclosed.", 4),
    ];

    // All eight approved reasons, including OtherApprovedReason.
    private static readonly (string Code, string Name)[] Reasons =
    [
        ("Overstock", "Overstock"),
        ("SupersededModel", "Superseded Model"),
        ("CustomerReturn", "Customer Return"),
        ("DisplayItem", "Display Item"),
        ("PackagingDamage", "Packaging Damage"),
        ("CosmeticDefect", "Cosmetic Defect"),
        ("MissingNonEssentialItem", "Missing Non-Essential Item"),
        ("OtherApprovedReason", "Other Approved Reason"),
    ];

    // Launch categories. Lower-level taxonomy is deferred.
    private static readonly (string Slug, string Name, int SortOrder)[] LaunchCategories =
    [
        ("small-kitchen-appliances", "Small Kitchen Appliances", 1),
        ("home-cleaning", "Home & Cleaning Appliances", 2),
        ("power-tools", "Power Tools & Workshop", 3),
    ];

    // Monthly only — no annual plans, discounts or trials (BUSINESS-MODEL.md §6.2).
    private static readonly (string Code, string Name, decimal MonthlyPriceJod, int ActiveListingQuota, bool HasFeaturedPlacement, int SortOrder)[] SubscriptionPlans =
    [
        ("Basic", "Basic", 35m, 15, false, 1),
        ("Standard", "Standard", 50m, 40, false, 2),
        ("Pro", "Pro", 80m, 120, true, 3),
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
        added += await SeedSubscriptionPlansAsync(db, cancellationToken);

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
        // Load the existing rows once and match every slug (root included) with the same
        // ordinal-ignore-case comparer, rather than delegating the root lookup to the
        // database where a case-sensitive server collation could miss it and let a second
        // root be inserted.
        var existing = await db.Categories.ToListAsync(cancellationToken);
        var existingSlugs = existing.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        var root = existing.FirstOrDefault(
            c => string.Equals(c.Slug, RootCategorySlug, StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            root = new Category("Open-Box & Ex-Display", RootCategorySlug, parentCategoryId: null, sortOrder: 0);
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

    private static async Task<int> SeedSubscriptionPlansAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existing = (await db.SubscriptionPlans.Select(p => p.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name, monthlyPriceJod, activeListingQuota, hasFeaturedPlacement, sortOrder) in SubscriptionPlans)
        {
            if (existing.Add(code))
            {
                db.SubscriptionPlans.Add(
                    new SubscriptionPlan(code, name, monthlyPriceJod, activeListingQuota, hasFeaturedPlacement, sortOrder));
                added++;
            }
        }

        return added;
    }
}
