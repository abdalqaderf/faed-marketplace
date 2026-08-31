using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// Physical-condition reference data (docs/01-PRD.md §6, docs/04-DOMAIN-MODEL.md §2). A DB
/// reference table rather than an enum so admins can manage the copy later
/// (docs/19-CODING-CONVENTIONS.md "Enums vs tables"). The Fashion MVP seeds grades A–D
/// only; there is no used-goods Grade E (docs/02-SCOPE-AND-DECISIONS.md,
/// docs/17-DATA-INVARIANTS.md).
///
/// Deliberately independent of <see cref="DiscountReason"/>: the physical state of an item
/// and the commercial reason it is discounted are separate concepts
/// (docs/adr/0003-CONDITION-VS-DISCOUNT-REASON.md).
/// </summary>
public class ConditionGrade
{
    public const int MaxCodeLength = 8;
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 512;

    private ConditionGrade()
    {
    }

    public ConditionGrade(string code, string name, string description, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Code = Require(code, nameof(code), MaxCodeLength);
        Name = Require(name, nameof(name), MaxNameLength);
        Description = Require(description, nameof(description), MaxDescriptionLength);
        SortOrder = sortOrder;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable natural key (<c>A</c>..<c>D</c>). Matched on when seeding.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    private static string Require(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"Condition grade {name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"Condition grade {name} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
