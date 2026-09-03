using Faed.Web.Services.Common;

namespace Faed.Web.Services.B2B;

/// <summary>
/// Accepted merchant-to-merchant deal use cases (tasks/TASK-008-B2B-DEALS.md,
/// docs/03-BUSINESS-RULES.md §10, docs/adr/0004-B2B-NEGOTIATION-SEPARATE-FROM-DEAL.md).
/// Accepting an offer revision atomically reserves every requested variant and creates the
/// deal; if any line cannot be reserved the acceptance fails as a whole
/// (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §6). Every method re-resolves the caller's
/// approved merchant and re-checks participation from the database — a guessed deal id
/// reveals nothing (docs/08-SECURITY-AND-PRIVACY.md §9,
/// docs/16-PERMISSIONS-MATRIX.md "View unrelated B2B negotiation — ❌").
/// </summary>
public interface IB2BDealService
{
    /// <summary>
    /// The addressed merchant accepts the current offer revision: reserves all lines
    /// atomically, moves the negotiation to <c>Accepted</c> and creates the deal, all in one
    /// transaction. Returns the new deal id.
    /// </summary>
    Task<Result<Guid>> AcceptOfferAsync(
        string merchantUserId, Guid negotiationId, AcceptOfferInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<B2BDealSummaryView>> GetMyDealsAsync(
        string merchantUserId, B2BDealFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many active deals need this merchant to act (seller fulfilment steps, buyer confirmation).</summary>
    Task<int> GetActionableDealCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default);

    /// <summary>The deal with its lines, or <c>null</c> when the caller is not one of its two merchants.</summary>
    Task<B2BDealDetailView?> GetDealAsync(
        string merchantUserId, Guid dealId, CancellationToken cancellationToken = default);

    /// <summary>Seller marks a direct-pickup deal ready for the buying merchant to collect.</summary>
    Task<Result> MarkReadyForPickupAsync(
        string merchantUserId, Guid dealId, CancellationToken cancellationToken = default);

    /// <summary>Seller marks a seller-arranged-shipping deal dispatched, optionally recording a shipment reference.</summary>
    Task<Result> MarkShippedAsync(
        string merchantUserId, Guid dealId, string? shipmentReference, CancellationToken cancellationToken = default);

    /// <summary>Seller records or updates the shipment reference for a seller-arranged-shipping deal.</summary>
    Task<Result> SetShipmentReferenceAsync(
        string merchantUserId, Guid dealId, string shipmentReference, CancellationToken cancellationToken = default);

    /// <summary>The buying merchant took delivery (collected the pickup or received the shipment).</summary>
    Task<Result> MarkDeliveredAsync(
        string merchantUserId, Guid dealId, CancellationToken cancellationToken = default);

    /// <summary>Either participant records the delivered deal as complete; reserved stock becomes sold.</summary>
    Task<Result> CompleteAsync(
        string merchantUserId, Guid dealId, CancellationToken cancellationToken = default);

    /// <summary>Either participant withdraws before the deal is delivered; reserved stock is released.</summary>
    Task<Result> CancelAsync(
        string merchantUserId, Guid dealId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the reserved stock of every deal whose reservation window lapsed before the
    /// seller started fulfilling it, and cancels the deal. Safe to call repeatedly — a deal
    /// already released is not processed again (docs/17-DATA-INVARIANTS.md "Reservation
    /// release is idempotent"). Returns the number released.
    /// </summary>
    Task<int> ReleaseExpiredDealReservationsAsync(CancellationToken cancellationToken = default);
}
