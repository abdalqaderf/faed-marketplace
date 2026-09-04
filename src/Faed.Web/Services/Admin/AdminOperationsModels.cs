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
    int OpenDisputes,
    int OrdersInProgress,
    int DealsAwaitingFulfillment,
    int OpenNegotiations,
    int InactiveCatalogItems);

// ---- Order / deal monitoring ---------------------------------------------------

public enum AdminOrderFilter
{
    /// <summary>Placed or in fulfilment — not yet in a terminal state.</summary>
    InProgress = 0,
    Completed = 1,
    Cancelled = 2,
    All = 3,
}

public enum AdminDealFilter
{
    /// <summary>Reserved stock, not yet completed or cancelled.</summary>
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
    decimal DeliveryFeeSnapshot,
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
    IReadOnlyList<AdminOrderLineView> Items,
    IReadOnlyList<AdminLinkedDisputeView> Disputes);

public sealed record AdminDealRow(
    Guid Id,
    DateTime CreatedAtUtc,
    B2BDealStatus Status,
    B2BFulfillmentType FulfillmentType,
    string SellingMerchantBusinessName,
    string BuyingMerchantBusinessName,
    int TotalUnits,
    decimal Total);

public sealed record AdminDealLineView(
    string VariantSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record AdminDealDetailView(
    Guid Id,
    B2BDealStatus Status,
    string? StatusReason,
    B2BFulfillmentType FulfillmentType,
    string? ShipmentReference,
    decimal Subtotal,
    decimal? ShippingCostSnapshot,
    decimal Total,
    string SellingMerchantBusinessName,
    string SellingMerchantSlug,
    string BuyingMerchantBusinessName,
    string BuyingMerchantSlug,
    string ListingTitle,
    string ListingSlug,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? ReservationExpiresAtUtc,
    IReadOnlyList<AdminDealLineView> Lines,
    IReadOnlyList<AdminLinkedDisputeView> Disputes);

/// <summary>A dispute attached to the order or deal being viewed, shown for context.</summary>
public sealed record AdminLinkedDisputeView(
    Guid Id,
    DisputeStatus Status,
    DisputeReasonCode ReasonCode,
    DateTime CreatedAtUtc);

// ---- Review monitoring --------------------------------------------------------

/// <summary>A row in the admin review monitor.</summary>
public sealed record AdminReviewRow(
    Guid Id,
    DateTime CreatedAtUtc,
    int Rating,
    string? Comment,
    string ReviewedMerchantBusinessName,
    string ReviewedMerchantSlug,
    TrustTransactionType TransactionType,
    Guid TransactionId);

// ---- Audit log ---------------------------------------------------------------

public enum AdminAuditLogFilter
{
    All = 0,
    Merchants = 1,
    Listings = 2,
    Disputes = 3,
    Catalog = 4,
}

/// <summary>A row in the admin audit-log viewer.</summary>
public sealed record AdminAuditLogRow(
    Guid Id,
    DateTime CreatedAtUtc,
    string AdminEmail,
    AdminActionType ActionType,
    string TargetType,
    string TargetId,
    string? Notes);
