using Faed.Web.Services.Common;

namespace Faed.Web.Services.Listings;

/// <summary>
/// Variant-level stock use cases. Inventory is separated
/// from listing editing because a quantity is not a claim about the product: adjusting stock
/// never sends a published listing back to moderation, while changing what the listing says
/// always does.
/// Every adjustment is written together with its <c>InventoryAdjustment</c> audit row inside
/// one transaction, under the variant's <c>rowversion</c>, so stock is never silently
/// overwritten.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// A bounded page of the caller's own variant stock, worst-stocked first — the row count
    /// grows with every listing/variant a merchant adds, so this is always a real page rather
    /// than the full set.
    /// </summary>
    Task<PagedResult<InventoryRow>> GetMyInventoryAsync(
        string userId, int page = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summary counts across the caller's <em>entire</em> inventory (not just the current
    /// page) for the dashboard stat row.
    /// </summary>
    Task<InventorySummary> GetMyInventorySummaryAsync(
        string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryAdjustmentView>> GetMyRecentAdjustmentsAsync(
        string userId, int take = 25, CancellationToken cancellationToken = default);

    /// <summary>Applies a signed stock correction to one of the caller's own variants.</summary>
    Task<Result<int>> AdjustStockAsync(
        string userId, StockAdjustmentInput input, CancellationToken cancellationToken = default);
}
