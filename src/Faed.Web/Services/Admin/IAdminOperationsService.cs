namespace Faed.Web.Services.Admin;

/// <summary>
/// Read-only projections that back the consolidated admin operational screens
/// (docs/07-UI-UX-SPEC.md §7, docs/10-IMPLEMENTATION-PLAN.md Phase 10,
/// tasks/TASK-010-ANALYTICS-AND-ADMIN.md). Nothing here mutates an order, a deal or a
/// review — an administrator monitors these for support, but B2C / B2B state transitions
/// stay with their participants (docs/16-PERMISSIONS-MATRIX.md). Every method is used only
/// behind the <c>AdminOnly</c> policy.
/// </summary>
public interface IAdminOperationsService
{
    Task<AdminDashboardView> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<AdminPagedResult<AdminOrderRow>> GetOrdersAsync(
        AdminOrderFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<AdminOrderDetailView?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<AdminPagedResult<AdminDealRow>> GetDealsAsync(
        AdminDealFilter filter, int page = 1, CancellationToken cancellationToken = default);

    Task<AdminDealDetailView?> GetDealAsync(Guid dealId, CancellationToken cancellationToken = default);

    Task<AdminPagedResult<AdminReviewRow>> GetReviewsAsync(
        int page = 1, CancellationToken cancellationToken = default);

    Task<AdminPagedResult<AdminAuditLogRow>> GetAuditLogAsync(
        AdminAuditLogFilter filter, int page = 1, CancellationToken cancellationToken = default);
}
