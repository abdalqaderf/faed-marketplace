using Faed.Web.Areas.Merchant.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Merchant.Controllers;

/// <summary>
/// Merchant-facing variant stock: the current level of every SKU and a manual-adjustment
/// form.
/// </summary>
[Area("Merchant")]
[Authorize(Policy = FaedPolicies.ApprovedMerchant)]
public sealed class InventoryController(IInventoryService inventory) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.RequireUserId();
        var rows = await inventory.GetMyInventoryAsync(userId, page, cancellationToken);
        var summary = await inventory.GetMyInventorySummaryAsync(userId, cancellationToken);
        var adjustments = await inventory.GetMyRecentAdjustmentsAsync(userId, cancellationToken: cancellationToken);
        return View(new InventoryPageModel { Rows = rows, Summary = summary, RecentAdjustments = adjustments });
    }

    [HttpPost]
    public async Task<IActionResult> Adjust(AdjustStockModel adjust, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await inventory.AdjustStockAsync(
                User.RequireUserId(),
                new StockAdjustmentInput(adjust.VariantId, adjust.AdjustmentType, adjust.QuantityDelta, adjust.Reason),
                cancellationToken);

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = $"Stock updated. New available quantity: {result.Value}.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        TempData["ErrorMessage"] = "Check the adjustment details and try again.";
        return RedirectToAction(nameof(Index));
    }
}
