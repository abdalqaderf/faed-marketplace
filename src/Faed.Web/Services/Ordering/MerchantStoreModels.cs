namespace Faed.Web.Services.Ordering;

// ---- Inputs ------------------------------------------------------------------------

public sealed record MerchantLocationInput(
    string Name,
    string AddressLine,
    string Area,
    string City,
    string? PickupInstructions,
    string? PickupHoursText);

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

/// <summary>Everything the merchant's Store Settings screen shows about fulfilment options.</summary>
public sealed record MerchantStoreSettingsView(
    IReadOnlyList<MerchantLocationView> Locations)
{
    public bool HasActiveFulfillment => Locations.Any(l => l.IsActive);
}
