using Faed.Web.Areas.Admin.ViewModels;
using Faed.Web.Authorization;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faed.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin catalog management: the taxonomy, condition grades, discount reasons and controlled
/// brands.
/// Every write goes through <see cref="IAdminCatalogService"/>, which re-checks the admin
/// role and records an audit entry.
/// </summary>
[Area("Admin")]
[Authorize(Policy = FaedPolicies.AdminOnly)]
public sealed class CatalogController(IAdminCatalogService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var overview = await catalog.GetOverviewAsync(cancellationToken);
        return View(new AdminCatalogPageModel { Catalog = overview });
    }

    // ---- Categories ----

    [HttpPost]
    public Task<IActionResult> CreateCategory(
        Guid? parentCategoryId, string name, int sortOrder, CancellationToken cancellationToken) =>
        After(catalog.CreateCategoryAsync(User.RequireUserId(), parentCategoryId, name ?? "", sortOrder, cancellationToken),
            "Category created.");

    [HttpPost]
    public Task<IActionResult> UpdateCategory(
        Guid id, string name, int sortOrder, CancellationToken cancellationToken) =>
        After(catalog.UpdateCategoryAsync(User.RequireUserId(), id, name ?? "", sortOrder, cancellationToken),
            "Category updated.");

    [HttpPost]
    public Task<IActionResult> SetCategoryActive(Guid id, bool isActive, CancellationToken cancellationToken) =>
        After(catalog.SetCategoryActiveAsync(User.RequireUserId(), id, isActive, cancellationToken),
            isActive ? "Category activated." : "Category deactivated.");

    // ---- Condition grades ----

    [HttpPost]
    public Task<IActionResult> UpdateConditionGrade(
        Guid id, string name, string description, int sortOrder, CancellationToken cancellationToken) =>
        After(catalog.UpdateConditionGradeAsync(User.RequireUserId(), id, name ?? "", description ?? "", sortOrder, cancellationToken),
            "Condition grade updated.");

    [HttpPost]
    public Task<IActionResult> SetConditionGradeActive(Guid id, bool isActive, CancellationToken cancellationToken) =>
        After(catalog.SetConditionGradeActiveAsync(User.RequireUserId(), id, isActive, cancellationToken),
            isActive ? "Condition grade activated." : "Condition grade deactivated.");

    // ---- Discount reasons ----

    [HttpPost]
    public Task<IActionResult> CreateDiscountReason(
        string code, string name, string? description, CancellationToken cancellationToken) =>
        After(catalog.CreateDiscountReasonAsync(User.RequireUserId(), code ?? "", name ?? "", description, cancellationToken),
            "Discount reason created.");

    [HttpPost]
    public Task<IActionResult> UpdateDiscountReason(
        Guid id, string name, string? description, CancellationToken cancellationToken) =>
        After(catalog.UpdateDiscountReasonAsync(User.RequireUserId(), id, name ?? "", description, cancellationToken),
            "Discount reason updated.");

    [HttpPost]
    public Task<IActionResult> SetDiscountReasonActive(Guid id, bool isActive, CancellationToken cancellationToken) =>
        After(catalog.SetDiscountReasonActiveAsync(User.RequireUserId(), id, isActive, cancellationToken),
            isActive ? "Discount reason activated." : "Discount reason deactivated.");

    // ---- Brands ----

    [HttpPost]
    public Task<IActionResult> CreateBrand(string name, CancellationToken cancellationToken) =>
        After(catalog.CreateBrandAsync(User.RequireUserId(), name ?? "", cancellationToken), "Brand created.");

    [HttpPost]
    public Task<IActionResult> RenameBrand(Guid id, string name, CancellationToken cancellationToken) =>
        After(catalog.RenameBrandAsync(User.RequireUserId(), id, name ?? "", cancellationToken), "Brand renamed.");

    [HttpPost]
    public Task<IActionResult> SetBrandActive(Guid id, bool isActive, CancellationToken cancellationToken) =>
        After(catalog.SetBrandActiveAsync(User.RequireUserId(), id, isActive, cancellationToken),
            isActive ? "Brand activated." : "Brand deactivated.");

    private async Task<IActionResult> After(Task<Result> action, string successMessage)
    {
        var result = await action;
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> After<T>(Task<Result<T>> action, string successMessage)
    {
        var result = await action;
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
