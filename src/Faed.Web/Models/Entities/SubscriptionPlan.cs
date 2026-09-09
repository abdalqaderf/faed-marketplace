using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A monthly plan a merchant subscribes to for the right to publish. Seeded reference data —
/// like <see cref="ConditionGrade"/> and <see cref="DiscountReason"/> — editable from the
/// admin console without a deployment. There is no free tier, no trial and no annual option:
/// every plan is a fixed monthly price and a fixed quota of listings that may be live at once.
/// </summary>
public class SubscriptionPlan
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 64;

    private SubscriptionPlan()
    {
    }

    public SubscriptionPlan(
        string code, string name, decimal monthlyPriceJod, int activeListingQuota,
        bool hasFeaturedPlacement, int sortOrder)
    {
        Id = Guid.CreateVersion7();
        Code = RequireCode(code);
        Name = RequireName(name);
        MonthlyPriceJod = RequirePositivePrice(monthlyPriceJod);
        ActiveListingQuota = RequirePositiveQuota(activeListingQuota);
        HasFeaturedPlacement = hasFeaturedPlacement;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable natural key (for example <c>Standard</c>). Matched on when seeding.</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public decimal MonthlyPriceJod { get; private set; }

    /// <summary>How many of this merchant's listings may be Live at the same time.</summary>
    public int ActiveListingQuota { get; private set; }

    public bool HasFeaturedPlacement { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Admin edit of the commercial terms. The <see cref="Code"/> is the stable natural key
    /// existing subscriptions reference, so it is never changed.
    /// </summary>
    public void UpdateDetails(
        string name, decimal monthlyPriceJod, int activeListingQuota, bool hasFeaturedPlacement, int sortOrder)
    {
        Name = RequireName(name);
        MonthlyPriceJod = RequirePositivePrice(monthlyPriceJod);
        ActiveListingQuota = RequirePositiveQuota(activeListingQuota);
        HasFeaturedPlacement = hasFeaturedPlacement;
        SortOrder = sortOrder;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string RequireCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCodeLength || value.Any(char.IsWhiteSpace))
        {
            throw new DomainException($"Plan code must be a single word of 1–{MaxCodeLength} characters.");
        }

        return value.Trim();
    }

    private static string RequireName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxNameLength)
        {
            throw new DomainException($"Plan name must be 1–{MaxNameLength} characters.");
        }

        return value.Trim();
    }

    private static decimal RequirePositivePrice(decimal value)
    {
        if (value < 0)
        {
            throw new DomainException("Plan price cannot be negative.");
        }

        return value;
    }

    private static int RequirePositiveQuota(int value)
    {
        if (value < 1)
        {
            throw new DomainException("A plan must allow at least one active listing.");
        }

        return value;
    }
}
