using Faed.Web.Services.Common;

namespace Faed.Web.Services.Admin;

/// <summary>
/// Read-only projections that back the consolidated admin operational screens.
/// Nothing here mutates an order, a deal or a
/// review — an administrator monitors these for support, but B2C / B2B state transitions
/// stay with their participants. Every method is used only
/// behind the <c>AdminOnly</c> policy.
/// </summary>
public interface IAdminOperationsService
{
    Task<AdminDashboardView> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AdminOrderRow>> GetOrdersAsync(
        AdminOrderFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<AdminOrderDetailView?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminDealRow>> GetDealsAsync(
        AdminDealFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<AdminDealDetailView?> GetDealAsync(Guid dealId, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminReviewRow>> GetReviewsAsync(
        int page = 1, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminAuditLogRow>> GetAuditLogAsync(
        AdminAuditLogFilter filter, int page = 1, CancellationToken cancellationToken = default);
}
