using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// The commercial/operational reason a merchant is selling below their normal channel.
/// A DB reference table rather than an
/// enum, and deliberately independent of
/// <see cref="ConditionGrade"/>. A listing
/// may carry more than one reason.
/// The MVP seeds all eight PRD-approved reasons, including <c>OtherApprovedReason</c>
/// </summary>
public class DiscountReason
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;

    private DiscountReason()
    {
    }

    public DiscountReason(string code, string name, string? description = null)
    {
        Id = Guid.CreateVersion7();
        Code = Require(code, nameof(code), MaxCodeLength);
        Name = Require(name, nameof(name), MaxNameLength);
        Description = Optional(description, nameof(description), MaxDescriptionLength);
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable natural key (for example <c>PastSeason</c>). Matched on when seeding.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Admin edit of the display fields. The <see cref="Code"/>
    /// is the stable natural key seeding and existing listings rely on, so it is never changed.
    /// </summary>
    public void UpdateDetails(string name, string? description)
    {
        Name = Require(name, nameof(name), MaxNameLength);
        Description = Optional(description, nameof(description), MaxDescriptionLength);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string Require(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"Discount reason {name} is required.");
        }

        return Optional(value, name, maxLength)!;
    }

    private static string? Optional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"Discount reason {name} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
