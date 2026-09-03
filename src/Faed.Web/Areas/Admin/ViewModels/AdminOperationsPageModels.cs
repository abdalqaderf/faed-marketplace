using Faed.Web.Services.Admin;
using Faed.Web.Services.Catalog;

namespace Faed.Web.Areas.Admin.ViewModels;

public sealed class AdminOverviewPageModel
{
    public required AdminDashboardView Dashboard { get; init; }
}

public sealed class AdminOrderMonitorPageModel
{
    public required AdminOrderFilter Filter { get; init; }

    public required AdminPagedResult<AdminOrderRow> Orders { get; init; }
}

public sealed class AdminOrderDetailPageModel
{
    public required AdminOrderDetailView Order { get; init; }
}

public sealed class AdminDealMonitorPageModel
{
    public required AdminDealFilter Filter { get; init; }

    public required AdminPagedResult<AdminDealRow> Deals { get; init; }
}

public sealed class AdminDealDetailPageModel
{
    public required AdminDealDetailView Deal { get; init; }
}

public sealed class AdminReviewMonitorPageModel
{
    public required AdminPagedResult<AdminReviewRow> Reviews { get; init; }
}

public sealed class AdminAuditLogPageModel
{
    public required AdminAuditLogFilter Filter { get; init; }

    public required AdminPagedResult<AdminAuditLogRow> Entries { get; init; }
}

public sealed class AdminPaginationPageModel
{
    public required string Action { get; init; }

    public string? Filter { get; init; }

    public required int Page { get; init; }

    public required int TotalPages { get; init; }

    public required int TotalCount { get; init; }

    public required int FirstItemNumber { get; init; }

    public required int LastItemNumber { get; init; }
}

public sealed class AdminCatalogPageModel
{
    public required AdminCatalogOverview Catalog { get; init; }
}
