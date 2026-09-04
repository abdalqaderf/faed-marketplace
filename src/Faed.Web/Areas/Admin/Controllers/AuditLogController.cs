using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin audit-log viewer. Read-only: <see cref="Faed.Web.Models.Entities.AdminActionLog"/> rows are append-only
/// and never edited or deleted.
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class AuditLogController(IAdminOperationsService operations) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        AdminAuditLogFilter filter = AdminAuditLogFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var rows = await operations.GetAuditLogAsync(filter, page, cancellationToken);
        return View(new AdminAuditLogPageModel { Filter = filter, Entries = rows });
    }
}
