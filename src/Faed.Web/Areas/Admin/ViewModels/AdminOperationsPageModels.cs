using Faed.Web.Services.Admin;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;

namespace Faed.Web.Areas.Admin.ViewModels;

public sealed class AdminOverviewPageModel
{
    public required AdminDashboardView Dashboard { get; init; }
}

public sealed class AdminOrderMonitorPageModel
{
    public required AdminOrderFilter Filter { get; init; }

    public required PagedResult<AdminOrderRow> Orders { get; init; }
}

public sealed class AdminOrderDetailPageModel
{
    public required AdminOrderDetailView Order { get; init; }
}

public sealed class AdminDealMonitorPageModel
{
    public required AdminDealFilter Filter { get; init; }

    public required PagedResult<AdminDealRow> Deals { get; init; }
}

public sealed class AdminDealDetailPageModel
{
    public required AdminDealDetailView Deal { get; init; }
}

public sealed class AdminReviewMonitorPageModel
{
    public required PagedResult<AdminReviewRow> Reviews { get; init; }
}

public sealed class AdminAuditLogPageModel
{
    public required AdminAuditLogFilter Filter { get; init; }

    public required PagedResult<AdminAuditLogRow> Entries { get; init; }
}

public sealed class AdminCatalogPageModel
{
    public required AdminCatalogOverview Catalog { get; init; }
}
