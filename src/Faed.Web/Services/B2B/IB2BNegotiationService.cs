using Faed.Web.Services.Common;

namespace Faed.Web.Services.B2B;

/// <summary>
/// Merchant-to-merchant negotiation use cases. Every method re-resolves the caller's approved merchant
/// and re-checks participation from the database — a guessed negotiation id reveals nothing.
/// Acceptance records the agreed revision only; the stock reservation
/// and the fulfillment deal are handled separately by the deal service.
/// </summary>
public interface IB2BNegotiationService
{
    /// <summary>The listing context for the "make an offer" form, or a failure when the
    /// caller cannot negotiate for this listing.</summary>
    Task<Result<OfferListingView>> GetListingForOfferAsync(
        string merchantUserId, string listingSlug, CancellationToken cancellationToken = default);

    /// <summary>Opens a negotiation with the buying merchant's first offer (revision 1).</summary>
    Task<Result<Guid>> StartNegotiationAsync(
        string merchantUserId, StartNegotiationInput input, CancellationToken cancellationToken = default);

    /// <summary>Adds a counter-offer as a new immutable revision.</summary>
    Task<Result> CounterOfferAsync(
        string merchantUserId, Guid negotiationId, CounterOfferInput input, CancellationToken cancellationToken = default);

    // Accepting the current revision is handled by <see cref="IB2BDealService.AcceptOfferAsync"/>:
    // acceptance atomically reserves stock and creates the B2BDeal in one transaction. The negotiation aggregate's own Accept transition is driven from there.

    Task<Result> RejectAsync(
        string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default);

    /// <summary>A participating merchant withdraws from the negotiation before it is accepted.</summary>
    Task<Result> CancelAsync(
        string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default);

    Task<PagedResult<B2BNegotiationSummaryView>> GetMyNegotiationsAsync(
        string merchantUserId, B2BNegotiationFilter filter, int page = 1, CancellationToken cancellationToken = default);

    /// <summary>How many open negotiations are waiting for this merchant to respond.</summary>
    Task<int> GetAwaitingResponseCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default);

    /// <summary>The negotiation with its full revision history, or <c>null</c> when the caller
    /// is not one of its two merchants.</summary>
    Task<B2BNegotiationDetailView?> GetNegotiationAsync(
        string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes every open negotiation whose current offer has lapsed. Safe to call repeatedly —
    /// a negotiation already closed is not processed again. Returns the number expired.
    /// </summary>
    Task<int> ExpireLapsedNegotiationsAsync(CancellationToken cancellationToken = default);
}
