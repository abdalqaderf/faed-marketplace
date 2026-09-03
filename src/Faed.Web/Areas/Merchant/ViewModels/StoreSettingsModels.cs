using System.ComponentModel.DataAnnotations;
using Faed.Web.Services.Ordering;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class PickupLocationFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter a name for this location.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the street address.")]
    [StringLength(300)]
    public string AddressLine { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the area.")]
    [StringLength(120)]
    public string Area { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the city.")]
    [StringLength(120)]
    public string City { get; set; } = "Amman";

    [StringLength(600)]
    public string? PickupInstructions { get; set; }

    [StringLength(300)]
    public string? PickupHoursText { get; set; }

    public MerchantLocationInput ToInput() =>
        new(Name, AddressLine, Area, City, PickupInstructions, PickupHoursText);
}

public sealed class DeliveryZoneFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter a name for this zone.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 9999.999, ErrorMessage = "Enter a delivery fee of zero or more.")]
    public decimal DeliveryFee { get; set; }

    [Range(0, 999999.999, ErrorMessage = "Enter a minimum order value of zero or more, or leave it blank.")]
    public decimal? MinimumOrderValue { get; set; }

    [StringLength(200)]
    public string? EstimatedDeliveryText { get; set; }

    public MerchantDeliveryZoneInput ToInput() =>
        new(Name, DeliveryFee, MinimumOrderValue, EstimatedDeliveryText);
}

public sealed class StoreSettingsPageModel
{
    public required MerchantStoreSettingsView Settings { get; init; }

    public PickupLocationFormModel LocationForm { get; set; } = new();

    public DeliveryZoneFormModel ZoneForm { get; set; } = new();
}
