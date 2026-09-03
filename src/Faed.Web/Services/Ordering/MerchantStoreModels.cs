namespace Faed.Web.Services.Ordering;

// ---- Inputs ------------------------------------------------------------------------

public sealed record MerchantLocationInput(
    string Name,
    string AddressLine,
    string Area,
    string City,
    string? PickupInstructions,
    string? PickupHoursText);

public sealed record MerchantDeliveryZoneInput(
    string Name,
    decimal DeliveryFee,
    decimal? MinimumOrderValue,
    string? EstimatedDeliveryText);

// ---- Views -----------------------------------------------------------------------

public sealed record MerchantLocationView(
    Guid Id,
    string Name,
    string AddressLine,
    string Area,
    string City,
    string? PickupInstructions,
    string? PickupHoursText,
    bool IsActive);

public sealed record MerchantDeliveryZoneView(
    Guid Id,
    string Name,
    decimal DeliveryFee,
    decimal? MinimumOrderValue,
    string? EstimatedDeliveryText,
    bool IsActive);

/// <summary>Everything the merchant's Store Settings screen shows about fulfilment options.</summary>
public sealed record MerchantStoreSettingsView(
    IReadOnlyList<MerchantLocationView> Locations,
    IReadOnlyList<MerchantDeliveryZoneView> DeliveryZones)
{
    public bool HasActiveFulfillment =>
        Locations.Any(l => l.IsActive) || DeliveryZones.Any(z => z.IsActive);
}
