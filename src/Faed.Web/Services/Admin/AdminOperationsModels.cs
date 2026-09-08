using Faed.Web.Models.Enums;

namespace Faed.Web.Services.Admin;

// Admin operational surfaces are paged with the shared Faed.Web.Services.Common.PagedResult<T>
// and Paging.AdminPageSize.

// ---- Dashboard -------------------------------------------------------------------

/// <summary>
/// Counts for the admin overview: what is waiting for a decision across every MVP queue.
/// Every number is a live query, never a stored aggregate.
/// </summary>
public sealed record AdminDashboardView(
    int MerchantsAwaitingReview,
    int ListingsAwaitingReview,
    int OrdersInProgress,
    int InactiveCatalogItems);

// ---- Order monitoring --------------------------------------------------------

public enum AdminOrderFilter
{
    /// <summary>Placed or in fulfilment — not yet in a terminal state.</summary>
    InProgress = 0,
    Completed = 1,
    Cancelled = 2,
    All = 3,
}

/// <summary>A row in the admin B2C order monitor.</summary>
public sealed record AdminOrderRow(
    Guid Id,
    DateTime CreatedAtUtc,
    OrderStatus Status,
    OrderFulfillmentType FulfillmentType,
    string MerchantBusinessName,
    string BuyerContactName,
    int TotalUnits,
    decimal Total);

public sealed record AdminOrderLineView(
    string ListingTitle,
    string VariantSnapshot,
    string ConditionSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>Read-only order detail for admin monitoring / support.</summary>
public sealed record AdminOrderDetailView(
    Guid Id,
    OrderStatus Status,
    string? StatusReason,
    OrderFulfillmentType FulfillmentType,
    string FulfillmentSnapshot,
    string? DeliveryAddressText,
    decimal Subtotal,
    decimal Total,
    string ContactName,
    string ContactPhone,
    string MerchantBusinessName,
    string MerchantSlug,
    string BuyerEmail,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? ReservationExpiresAtUtc,
    IReadOnlyList<AdminOrderLineView> Items);

// ---- Review monitoring --------------------------------------------------------

/// <summary>A row in the admin review monitor.</summary>
public sealed record AdminReviewRow(
    Guid Id,
    DateTime CreatedAtUtc,
    int Rating,
    string? Comment,
    string ReviewedMerchantBusinessName,
    string ReviewedMerchantSlug,
    Guid OrderId);
