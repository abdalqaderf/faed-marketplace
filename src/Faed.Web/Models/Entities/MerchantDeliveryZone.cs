using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A geographic area a merchant will deliver B2C orders to, with its own fee and optional
/// minimum order value. Faed
/// stores the fee and estimate but never books or prices carriage itself.
/// The fee in force at checkout is snapshotted onto the order.
/// </summary>
public class MerchantDeliveryZone
{
    public const int MaxNameLength = 120;
    public const int MaxEstimateLength = 200;

    private MerchantDeliveryZone()
    {
    }

    public MerchantDeliveryZone(
        Guid merchantProfileId,
        string name,
        decimal deliveryFee,
        decimal? minimumOrderValue,
        string? estimatedDeliveryText,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        MerchantProfileId = merchantProfileId;
        Name = Require(name, "zone name", MaxNameLength);
        DeliveryFee = RequireMoney(deliveryFee, "delivery fee");
        MinimumOrderValue = minimumOrderValue is { } min ? RequireMoney(min, "minimum order value") : null;
        EstimatedDeliveryText = Optional(estimatedDeliveryText, "delivery estimate", MaxEstimateLength);
        IsActive = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid MerchantProfileId { get; private set; }

    public string Name { get; private set; } = null!;

    public decimal DeliveryFee { get; private set; }

    public decimal? MinimumOrderValue { get; private set; }

    public string? EstimatedDeliveryText { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        decimal deliveryFee,
        decimal? minimumOrderValue,
        string? estimatedDeliveryText,
        DateTime nowUtc)
    {
        Name = Require(name, "zone name", MaxNameLength);
        DeliveryFee = RequireMoney(deliveryFee, "delivery fee");
        MinimumOrderValue = minimumOrderValue is { } min ? RequireMoney(min, "minimum order value") : null;
        EstimatedDeliveryText = Optional(estimatedDeliveryText, "delivery estimate", MaxEstimateLength);
        UpdatedAtUtc = nowUtc;
    }

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    private static string Require(string value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException($"The {field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static decimal RequireMoney(decimal value, string field)
    {
        if (value < 0)
        {
            throw new DomainException($"The {field} cannot be negative.");
        }

        return value;
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
