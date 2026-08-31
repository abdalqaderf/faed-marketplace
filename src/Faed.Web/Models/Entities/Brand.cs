using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// An optional controlled brand (docs/04-DOMAIN-MODEL.md §2). Admin-managed only in the
/// MVP — merchants choose from the controlled list, they never create brands
/// (tasks/TASK-003-CATALOG.md, docs/13-OPEN-QUESTIONS.md items 5–6). No brands are seeded;
/// the table exists so listings can reference one when catalog rules later require it.
///
/// <see cref="Slug"/> is a display/routing identifier only, never an authorization key
/// (docs/06-ARCHITECTURE.md §12).
/// </summary>
public class Brand
{
    public const int MaxNameLength = 128;
    public const int MaxSlugLength = 160;

    private Brand()
    {
    }

    public Brand(string name, string slug)
    {
        Id = Guid.CreateVersion7();
        Name = Require(name, nameof(name), MaxNameLength);
        Slug = Require(slug, nameof(slug), MaxSlugLength);
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public bool IsActive { get; private set; }

    private static string Require(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"Brand {name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"Brand {name} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
