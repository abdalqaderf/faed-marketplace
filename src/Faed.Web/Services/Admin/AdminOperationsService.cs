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

        var ordersInProgress = await db.Orders
            .AsNoTracking()
            .CountAsync(
                o => o.Status != OrderStatus.Completed
                    && o.Status != OrderStatus.Cancelled
                    && o.Status != OrderStatus.NoShow,
                cancellationToken);

        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: false, includeRoot: true, cancellationToken);
        var inactiveCatalog =
            await db.Categories.AsNoTracking()
                .CountAsync(c => launchCategoryIds.Contains(c.Id) && !c.IsActive, cancellationToken)
            + await db.DiscountReasons.AsNoTracking().CountAsync(r => !r.IsActive, cancellationToken)
            + await db.ConditionGrades.AsNoTracking().CountAsync(g => !g.IsActive, cancellationToken);

        return new AdminDashboardView(
            merchantsAwaiting, listingsAwaiting,
            ordersInProgress, inactiveCatalog);
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

        return new AdminOrderDetailView(
            row.o.Id,
            row.o.Status,
            row.o.StatusReason,
            row.o.FulfillmentType,
            row.o.FulfillmentSnapshot,
            row.o.DeliveryAddressText,
            row.o.Subtotal,
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
                .ToList());
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
                x.Review.OrderId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReviewRow>(rows, totalCount, page, Paging.AdminPageSize);
    }

    // ---- Internals -----------------------------------------------------

    private static int NormalizePage(int requestedPage, int totalCount)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)Paging.AdminPageSize));
        return Math.Clamp(requestedPage, 1, totalPages);
    }
}
