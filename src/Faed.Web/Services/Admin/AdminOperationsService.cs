using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Admin;

/// <inheritdoc />
public sealed class AdminOperationsService(IApplicationDbContext db) : IAdminOperationsService
{
    public async Task<AdminDashboardView> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var merchantsAwaiting = await db.MerchantProfiles
            .AsNoTracking()
            .CountAsync(m => m.VerificationStatus == MerchantVerificationStatus.PendingReview, cancellationToken);

        var listingsAwaiting = await db.Listings
            .AsNoTracking()
            .CountAsync(l => l.Status == ListingStatus.PendingReview, cancellationToken);

        var openDisputes = await db.Disputes
            .AsNoTracking()
            .CountAsync(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview, cancellationToken);

        var ordersInProgress = await db.Orders
            .AsNoTracking()
            .CountAsync(
                o => o.Status != OrderStatus.Completed
                    && o.Status != OrderStatus.Cancelled
                    && o.Status != OrderStatus.NoShow,
                cancellationToken);

        var dealsAwaiting = await db.B2BDeals
            .AsNoTracking()
            .CountAsync(d => d.Status == B2BDealStatus.AwaitingFulfillment, cancellationToken);

        var openNegotiations = await db.B2BNegotiations
            .AsNoTracking()
            .CountAsync(n => n.Status == B2BNegotiationStatus.Open, cancellationToken);

        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: false, includeRoot: true, cancellationToken);
        var inactiveCatalog =
            await db.Categories.AsNoTracking()
                .CountAsync(c => launchCategoryIds.Contains(c.Id) && !c.IsActive, cancellationToken)
            + await db.DiscountReasons.AsNoTracking().CountAsync(r => !r.IsActive, cancellationToken)
            + await db.ConditionGrades.AsNoTracking().CountAsync(g => !g.IsActive, cancellationToken)
            + await db.Brands.AsNoTracking().CountAsync(b => !b.IsActive, cancellationToken);

        return new AdminDashboardView(
            merchantsAwaiting, listingsAwaiting, openDisputes,
            ordersInProgress, dealsAwaiting, openNegotiations, inactiveCatalog);
    }

    // ---- Orders ---------------------------------------------------------------

    public async Task<PagedResult<AdminOrderRow>> GetOrdersAsync(
        AdminOrderFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query =
            from o in db.Orders.AsNoTracking()
            join m in db.MerchantProfiles.AsNoTracking() on o.MerchantProfileId equals m.Id
            select new { o, m.BusinessName };

        query = filter switch
        {
            AdminOrderFilter.InProgress => query.Where(x =>
                x.o.Status != OrderStatus.Completed
                && x.o.Status != OrderStatus.Cancelled
                && x.o.Status != OrderStatus.NoShow),
            AdminOrderFilter.Completed => query.Where(x => x.o.Status == OrderStatus.Completed),
            AdminOrderFilter.Cancelled => query.Where(x =>
                x.o.Status == OrderStatus.Cancelled || x.o.Status == OrderStatus.NoShow),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);
        page = NormalizePage(page, totalCount);
        var rows = await query
            .OrderByDescending(x => x.o.CreatedAtUtc)
            .ThenByDescending(x => x.o.Id)
            .Skip((page - 1) * Paging.AdminPageSize)
            .Take(Paging.AdminPageSize)
            .Select(x => new AdminOrderRow(
                x.o.Id,
                x.o.CreatedAtUtc,
                x.o.Status,
                x.o.FulfillmentType,
                x.BusinessName,
                x.o.ContactName,
                x.o.Items.Sum(i => i.Quantity),
                x.o.Total))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminOrderRow>(rows, totalCount, page, Paging.AdminPageSize);
    }

    public async Task<AdminOrderDetailView?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var row = await (
            from o in db.Orders.AsNoTracking().Include(o => o.Items)
            where o.Id == orderId
            join m in db.MerchantProfiles.AsNoTracking() on o.MerchantProfileId equals m.Id
            join u in db.Users.AsNoTracking() on o.BuyerUserId equals u.Id into bu
            from u in bu.DefaultIfEmpty()
            select new { o, m.BusinessName, MerchantSlug = m.PublicSlug, BuyerEmail = u != null ? u.Email : null })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var disputes = await GetLinkedDisputesForOrderAsync(orderId, cancellationToken);

        return new AdminOrderDetailView(
            row.o.Id,
            row.o.Status,
            row.o.StatusReason,
            row.o.FulfillmentType,
            row.o.FulfillmentSnapshot,
            row.o.DeliveryAddressText,
            row.o.Subtotal,
            row.o.DeliveryFeeSnapshot,
            row.o.Total,
            row.o.ContactName,
            row.o.ContactPhone,
            row.BusinessName,
            row.MerchantSlug,
            row.BuyerEmail ?? "(account removed)",
            row.o.CreatedAtUtc,
            row.o.ConfirmedAtUtc,
            row.o.CompletedAtUtc,
            row.o.CancelledAtUtc,
            row.o.ReservationExpiresAtUtc,
            row.o.Items
                .OrderBy(i => i.ListingTitleSnapshot)
                .Select(i => new AdminOrderLineView(
                    i.ListingTitleSnapshot, i.VariantSnapshot, i.ConditionGradeSnapshot,
                    i.Quantity, i.UnitPriceSnapshot, i.LineTotalSnapshot))
                .ToList(),
            disputes);
    }

    // ---- Deals ---------------------------------------------------------------

    public async Task<PagedResult<AdminDealRow>> GetDealsAsync(
        AdminDealFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query =
            from d in db.B2BDeals.AsNoTracking()
            join s in db.MerchantProfiles.AsNoTracking() on d.SellingMerchantProfileId equals s.Id
            join b in db.MerchantProfiles.AsNoTracking() on d.BuyingMerchantProfileId equals b.Id
            select new { d, Seller = s.BusinessName, Buyer = b.BusinessName };

        query = filter switch
        {
            AdminDealFilter.InProgress => query.Where(x =>
                x.d.Status != B2BDealStatus.Completed && x.d.Status != B2BDealStatus.Cancelled),
            AdminDealFilter.Completed => query.Where(x => x.d.Status == B2BDealStatus.Completed),
            AdminDealFilter.Cancelled => query.Where(x => x.d.Status == B2BDealStatus.Cancelled),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);
        page = NormalizePage(page, totalCount);
        var rows = await query
            .OrderByDescending(x => x.d.CreatedAtUtc)
            .ThenByDescending(x => x.d.Id)
            .Skip((page - 1) * Paging.AdminPageSize)
            .Take(Paging.AdminPageSize)
            .Select(x => new AdminDealRow(
                x.d.Id,
                x.d.CreatedAtUtc,
                x.d.Status,
                x.d.FulfillmentType,
                x.Seller,
                x.Buyer,
                x.d.Lines.Sum(l => l.Quantity),
                x.d.TotalSnapshot))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminDealRow>(rows, totalCount, page, Paging.AdminPageSize);
    }

    public async Task<AdminDealDetailView?> GetDealAsync(Guid dealId, CancellationToken cancellationToken = default)
    {
        var row = await (
            from d in db.B2BDeals.AsNoTracking().Include(d => d.Lines)
            where d.Id == dealId
            join s in db.MerchantProfiles.AsNoTracking() on d.SellingMerchantProfileId equals s.Id
            join b in db.MerchantProfiles.AsNoTracking() on d.BuyingMerchantProfileId equals b.Id
            join n in db.B2BNegotiations.AsNoTracking() on d.B2BNegotiationId equals n.Id
            join l in db.Listings.AsNoTracking() on n.ListingId equals l.Id
            select new
            {
                d,
                Seller = s.BusinessName,
                SellerSlug = s.PublicSlug,
                Buyer = b.BusinessName,
                BuyerSlug = b.PublicSlug,
                ListingTitle = l.Title,
                ListingSlug = l.Slug,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var disputes = await GetLinkedDisputesForDealAsync(dealId, cancellationToken);

        return new AdminDealDetailView(
            row.d.Id,
            row.d.Status,
            row.d.StatusReason,
            row.d.FulfillmentType,
            row.d.ShipmentReference,
            row.d.SubtotalSnapshot,
            row.d.ShippingCostSnapshot,
            row.d.TotalSnapshot,
            row.Seller,
            row.SellerSlug,
            row.Buyer,
            row.BuyerSlug,
            row.ListingTitle,
            row.ListingSlug,
            row.d.CreatedAtUtc,
            row.d.CompletedAtUtc,
            row.d.CancelledAtUtc,
            row.d.ReservationExpiresAtUtc,
            row.d.Lines
                .OrderBy(l => l.VariantSnapshot)
                .Select(l => new AdminDealLineView(
                    l.VariantSnapshot, l.Quantity, l.UnitPriceSnapshot, l.LineTotalSnapshot))
                .ToList(),
            disputes);
    }

    // ---- Reviews -----------------------------------------------------------

    public async Task<PagedResult<AdminReviewRow>> GetReviewsAsync(
        int page = 1, CancellationToken cancellationToken = default)
    {
        var query =
            from r in db.Reviews.AsNoTracking()
            join m in db.MerchantProfiles.AsNoTracking() on r.ReviewedMerchantProfileId equals m.Id
            select new { Review = r, m.BusinessName, m.PublicSlug };

        var totalCount = await query.CountAsync(cancellationToken);
        page = NormalizePage(page, totalCount);
        var rows = await query
            .OrderByDescending(x => x.Review.CreatedAtUtc)
            .ThenByDescending(x => x.Review.Id)
            .Skip((page - 1) * Paging.AdminPageSize)
            .Take(Paging.AdminPageSize)
            .Select(x => new AdminReviewRow(
                x.Review.Id,
                x.Review.CreatedAtUtc,
                x.Review.Rating,
                x.Review.Comment,
                x.BusinessName,
                x.PublicSlug,
                x.Review.OrderId != null ? TrustTransactionType.B2COrder : TrustTransactionType.B2BDeal,
                x.Review.OrderId != null ? x.Review.OrderId.Value : x.Review.B2BDealId!.Value))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReviewRow>(rows, totalCount, page, Paging.AdminPageSize);
    }

    // ---- Audit log -------------------------------------------------------

    public async Task<PagedResult<AdminAuditLogRow>> GetAuditLogAsync(
        AdminAuditLogFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query =
            from log in db.AdminActionLogs.AsNoTracking()
            join u in db.Users.AsNoTracking() on log.AdminUserId equals u.Id into au
            from u in au.DefaultIfEmpty()
            select new { log, Email = u != null ? u.Email : null };

        query = filter switch
        {
            AdminAuditLogFilter.Merchants => query.Where(x =>
                x.log.ActionType == AdminActionType.MerchantApproved
                || x.log.ActionType == AdminActionType.MerchantRejected
                || x.log.ActionType == AdminActionType.MerchantSuspended
                || x.log.ActionType == AdminActionType.MerchantReinstated
                || x.log.ActionType == AdminActionType.MerchantVerificationDocumentAccessed),
            AdminAuditLogFilter.Listings => query.Where(x =>
                x.log.ActionType == AdminActionType.ListingApproved
                || x.log.ActionType == AdminActionType.ListingRejected
                || x.log.ActionType == AdminActionType.ListingHidden
                || x.log.ActionType == AdminActionType.ListingRestored),
            AdminAuditLogFilter.Disputes => query.Where(x =>
                x.log.ActionType == AdminActionType.DisputeReviewStarted
                || x.log.ActionType == AdminActionType.DisputeResolved
                || x.log.ActionType == AdminActionType.DisputeRejected
                || x.log.ActionType == AdminActionType.DisputeEvidenceAccessed),
            AdminAuditLogFilter.Catalog => query.Where(x =>
                x.log.ActionType == AdminActionType.CatalogItemCreated
                || x.log.ActionType == AdminActionType.CatalogItemUpdated
                || x.log.ActionType == AdminActionType.CatalogItemAvailabilityChanged),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);
        page = NormalizePage(page, totalCount);
        var rows = await query
            .OrderByDescending(x => x.log.CreatedAtUtc)
            .ThenByDescending(x => x.log.Id)
            .Skip((page - 1) * Paging.AdminPageSize)
            .Take(Paging.AdminPageSize)
            .Select(x => new AdminAuditLogRow(
                x.log.Id,
                x.log.CreatedAtUtc,
                x.Email ?? x.log.AdminUserId,
                x.log.ActionType,
                x.log.TargetType,
                x.log.TargetId,
                x.log.Notes))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminAuditLogRow>(rows, totalCount, page, Paging.AdminPageSize);
    }

    // ---- Internals -----------------------------------------------------

    private async Task<IReadOnlyList<AdminLinkedDisputeView>> GetLinkedDisputesForOrderAsync(
        Guid orderId, CancellationToken cancellationToken) =>
        await db.Disputes.AsNoTracking()
            .Where(d => d.OrderId == orderId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new AdminLinkedDisputeView(d.Id, d.Status, d.ReasonCode, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<AdminLinkedDisputeView>> GetLinkedDisputesForDealAsync(
        Guid dealId, CancellationToken cancellationToken) =>
        await db.Disputes.AsNoTracking()
            .Where(d => d.B2BDealId == dealId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new AdminLinkedDisputeView(d.Id, d.Status, d.ReasonCode, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    private static int NormalizePage(int requestedPage, int totalCount)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)Paging.AdminPageSize));
        return Math.Clamp(requestedPage, 1, totalPages);
    }
}
