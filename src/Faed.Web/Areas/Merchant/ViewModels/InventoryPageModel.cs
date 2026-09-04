using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class InventoryPageModel
{
    public required PagedResult<InventoryRow> Rows { get; init; }

    public required InventorySummary Summary { get; init; }

    public required IReadOnlyList<InventoryAdjustmentView> RecentAdjustments { get; init; }

    public AdjustStockModel Adjust { get; init; } = new();
}

public sealed class AdjustStockModel
{
    [Required]
    public Guid VariantId { get; set; }

    [Required]
    [EnumDataType(typeof(InventoryAdjustmentType), ErrorMessage = "Choose a valid reason type.")]
    [Display(Name = "Adjustment type")]
    public InventoryAdjustmentType AdjustmentType { get; set; } = InventoryAdjustmentType.ManualCorrection;

    [Required(ErrorMessage = "Enter how many units to add or remove.")]
    [Range(-1_000_000, 1_000_000)]
    [Display(Name = "Quantity change")]
    public int QuantityDelta { get; set; }

    [Required(ErrorMessage = "Explain why the stock is changing.")]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}
