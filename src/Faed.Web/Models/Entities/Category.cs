using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A node in the marketplace taxonomy (docs/04-DOMAIN-MODEL.md §2). The tree is generic so
/// future sectors are added as data, never as new schema (AGENTS.md §3,
/// docs/14-FUTURE-EXPANSION.md). The MVP seeds only the <c>Fashion Overstock</c> root and
/// the three launch categories; deeper taxonomy is deferred (tasks/TASK-003-CATALOG.md,
/// docs/13-OPEN-QUESTIONS.md item 4).
///
/// <see cref="Slug"/> is a display/routing identifier only, never an authorization key
/// (docs/06-ARCHITECTURE.md §12).
/// </summary>
public class Category
{
    public const int MaxNameLength = 128;
    public const int MaxSlugLength = 160;

    private readonly List<Category> _children = [];

    private Category()
    {
    }

    public Category(string name, string slug, Guid? parentCategoryId, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Name = Require(name, nameof(name), MaxNameLength);
        Slug = Require(slug, nameof(slug), MaxSlugLength);
        ParentCategoryId = parentCategoryId;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Null for a sector root; otherwise the parent node.</summary>
    public Guid? ParentCategoryId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Category? Parent { get; private set; }

    public IReadOnlyCollection<Category> Children => _children.AsReadOnly();

    public bool IsRoot => ParentCategoryId is null;

    private static string Require(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"Category {name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"Category {name} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
