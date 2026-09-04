using Faed.Web.Services.Common;

namespace Faed.Web.Services.Ordering;

/// <summary>
/// B2C ordering use cases (tasks/TASK-006-B2C-ORDERS.md). Reservation, cancellation,
/// completion and expiry each run inside an explicit transaction so stock and order state
/// move together or not at all (docs/06-ARCHITECTURE.md §6). Every method re-resolves the
/// caller's buyer identity or merchant ownership from the database — a guessed order id is
/// never enough (docs/08-SECURITY-AND-PRIVACY.md §9).
/// </summary>
public interface IOrderService
{
    /// <summary>The single-listing order builder, or a failure when the listing is not
    /// publicly purchasable.</summary>
    Task<Result<CheckoutView>> GetCheckoutAsync(
        string buyerUserId, string listingSlug, CancellationToken cancellationToken = default);

    /// <summary>Reserves stock and creates the order atomically, or fails without side effects.</summary>
    Task<Result<Guid>> PlaceOrderAsync(
        string buyerUserId, PlaceOrderInput input, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderSummaryView>> GetMyOrdersAsync(
        string buyerUserId, int page = 1, CancellationToken cancellationToken = default);

    /// <summary>The buyer's own order, or <c>null</c> when it is not theirs.</summary>
    Task<OrderDetailView?> GetMyOrderAsync(
        string buyerUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> CancelMyOrderAsync(
        string buyerUserId, Guid orderId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// The buyer confirms they received the order, moving it to <c>Completed</c> and its
    /// reserved stock to sold (docs/01-PRD.md §4 "confirm receipt",
    /// docs/03-BUSINESS-RULES.md §7). Only valid from <c>ReadyForPickup</c> / <c>OutForDelivery</c>.
    /// </summary>
    Task<Result> ConfirmReceiptAsync(
        string buyerUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<OrderSummaryView>> GetMerchantOrdersAsync(
        string merchantUserId, MerchantOrderFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<int> GetMerchantOpenOrderCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default);

    /// <summary>An order owned by the caller's merchant, or <c>null</c>.</summary>
    Task<OrderDetailView?> GetMerchantOrderAsync(
        string merchantUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> ConfirmAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> MarkReadyForPickupAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> MarkOutForDeliveryAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> CompleteAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default);

    Task<Result> MarkNoShowAsync(
        string merchantUserId, Guid orderId, string reason, CancellationToken cancellationToken = default);

    Task<Result> CancelAsMerchantAsync(
        string merchantUserId, Guid orderId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the reserved stock of every <see cref="Models.Enums.OrderStatus.Pending"/> order
    /// whose reservation window has elapsed and cancels it. Safe to call repeatedly — an order
    /// already released is not processed again (docs/03-BUSINESS-RULES.md §7,
    /// docs/09-TEST-STRATEGY.md "repeated expiry job is idempotent"). Returns the number of
    /// orders released.
    /// </summary>
    Task<int> ReleaseExpiredReservationsAsync(CancellationToken cancellationToken = default);
}
