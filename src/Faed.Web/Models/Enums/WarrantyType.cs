namespace Faed.Web.Models.Enums;

/// <summary>Whether an appliance still carries a warranty, and whose.</summary>
public enum WarrantyType
{
    None = 0,

    /// <summary>The original manufacturer's warranty is still valid.</summary>
    ManufacturerWarranty = 1,

    /// <summary>The merchant's own shop warranty, separate from the manufacturer's.</summary>
    ShopWarranty = 2,
}
