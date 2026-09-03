using Faed.Web.Data.Seed;
using Faed.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Catalog;

/// <summary>
/// Resolves the category tree that belongs to the Fashion Overstock launch sector. The
/// taxonomy remains multi-sector capable, but only descendants of the seeded launch root
/// are eligible in the MVP UI (AGENTS.md section 3, docs/14-FUTURE-EXPANSION.md).
/// </summary>
public static class LaunchCatalogScope
{
    public static async Task<IReadOnlySet<Guid>> GetCategoryIdsAsync(
        IApplicationDbContext db,
        bool activeOnly,
        bool includeRoot,
        CancellationToken cancellationToken = default)
    {
        var query = db.Categories.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .Select(c => new CategoryLink(c.Id, c.ParentCategoryId, c.Slug))
            .ToListAsync(cancellationToken);

        var root = categories.FirstOrDefault(c => string.Equals(
            c.Slug,
            CatalogDataSeeder.RootCategorySlug,
            StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            return new HashSet<Guid>();
        }

        var result = new HashSet<Guid>();
        if (includeRoot)
        {
            result.Add(root.Id);
        }

        var childrenByParent = categories.ToLookup(c => c.ParentCategoryId);
        var frontier = new Queue<Guid>();
        frontier.Enqueue(root.Id);
        while (frontier.Count > 0)
        {
            var parentId = frontier.Dequeue();
            foreach (var child in childrenByParent[parentId])
            {
                if (result.Add(child.Id))
                {
                    frontier.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    private sealed record CategoryLink(Guid Id, Guid? ParentCategoryId, string Slug);
}
