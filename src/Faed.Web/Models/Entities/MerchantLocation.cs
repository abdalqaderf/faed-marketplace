using Faed.Web.Models;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A physical pickup point a merchant offers for B2C orders (docs/03-BUSINESS-RULES.md §12,
/// docs/04-DOMAIN-MODEL.md §1). Faed does not operate warehouses; the address, instructions
/// and hours are merchant-supplied free text and are snapshotted onto the order at checkout.
/// </summary>
public class MerchantLocation
{
    public const int MaxNameLength = 120;
    public const int MaxAddressLineLength = 300;
    public const int MaxAreaLength = 120;
    public const int MaxCityLength = 120;
    public const int MaxInstructionsLength = 600;
    public const int MaxHoursLength = 300;

    private MerchantLocation()
    {
    }

    public MerchantLocation(
        Guid merchantProfileId,
        string name,
        string addressLine,
        string area,
        string city,
        string? pickupInstructions,
        string? pickupHoursText,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        MerchantProfileId = merchantProfileId;
        Name = Require(name, "location name", MaxNameLength);
        AddressLine = Require(addressLine, "address", MaxAddressLineLength);
        Area = Require(area, "area", MaxAreaLength);
        City = Require(city, "city", MaxCityLength);
        PickupInstructions = Optional(pickupInstructions, "pickup instructions", MaxInstructionsLength);
        PickupHoursText = Optional(pickupHoursText, "pickup hours", MaxHoursLength);
        IsActive = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public Guid MerchantProfileId { get; private set; }

    public string Name { get; private set; } = null!;

    public string AddressLine { get; private set; } = null!;

    public string Area { get; private set; } = null!;

    public string City { get; private set; } = null!;

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public string? PickupInstructions { get; private set; }

    public string? PickupHoursText { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string addressLine,
        string area,
        string city,
        string? pickupInstructions,
        string? pickupHoursText,
        DateTime nowUtc)
    {
        Name = Require(name, "location name", MaxNameLength);
        AddressLine = Require(addressLine, "address", MaxAddressLineLength);
        Area = Require(area, "area", MaxAreaLength);
        City = Require(city, "city", MaxCityLength);
        PickupInstructions = Optional(pickupInstructions, "pickup instructions", MaxInstructionsLength);
        PickupHoursText = Optional(pickupHoursText, "pickup hours", MaxHoursLength);
        UpdatedAtUtc = nowUtc;
    }

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>A one-line address for order snapshots and buyer confirmation screens.</summary>
    public string DescribeAddress() => $"{Name} — {AddressLine}, {Area}, {City}";

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
