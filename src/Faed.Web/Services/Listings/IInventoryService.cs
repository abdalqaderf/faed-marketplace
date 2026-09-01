using Faed.Web.Services.Common;

namespace Faed.Web.Services.Listings;

/// <summary>
/// Variant-level stock use cases (AGENTS.md Rule A, docs/adr/0002). Inventory is separated
/// from listing editing because a quantity is not a claim about the product: adjusting stock
/// never sends a published listing back to moderation, while changing what the listing says
/// always does.
///
/// Every adjustment is written together with its <c>InventoryAdjustment</c> audit row inside
/// one transaction, under the variant's <c>rowversion</c>, so stock is never silently
/// overwritten (docs/03-BUSINESS-RULES.md §6, AGENTS.md §7).
/// </summary>
public interface IInventoryService
{
    Task<IReadOnlyList<InventoryRow>> GetMyInventoryAsync(
        string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryAdjustmentView>> GetMyRecentAdjustmentsAsync(
        string userId, int take = 25, CancellationToken cancellationToken = default);

    /// <summary>Applies a signed stock correction to one of the caller's own variants.</summary>
    Task<Result<int>> AdjustStockAsync(
        string userId, StockAdjustmentInput input, CancellationToken cancellationToken = default);
}
