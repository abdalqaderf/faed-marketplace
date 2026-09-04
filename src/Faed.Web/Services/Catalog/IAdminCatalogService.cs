using Faed.Web.Services.Common;

namespace Faed.Web.Services.Catalog;

/// <summary>
/// Admin management of the catalog reference data — the taxonomy, condition grades, discount
/// reasons and controlled brands.
/// Reference rows are never deleted — an unused sector, grade or reason is deactivated so
/// existing listings that reference it keep working. The
/// stable natural keys (<c>Code</c>, <c>Slug</c>) the seeder and existing listings depend on
/// are immutable; only display fields and availability change. Every change re-checks the
/// admin role and is written with an <c>AdminActionLog</c> entry in one transaction
/// </summary>
public interface IAdminCatalogService
{
    Task<AdminCatalogOverview> GetOverviewAsync(CancellationToken cancellationToken = default);

    // ---- Categories ----
    Task<Result<Guid>> CreateCategoryAsync(
        string adminUserId, Guid? parentCategoryId, string name, int sortOrder, CancellationToken cancellationToken = default);

    Task<Result> UpdateCategoryAsync(
        string adminUserId, Guid categoryId, string name, int sortOrder, CancellationToken cancellationToken = default);

    Task<Result> SetCategoryActiveAsync(
        string adminUserId, Guid categoryId, bool isActive, CancellationToken cancellationToken = default);

    // ---- Condition grades (A–D fixed; copy and availability only) ----
    Task<Result> UpdateConditionGradeAsync(
        string adminUserId, Guid conditionGradeId, string name, string description, int sortOrder, CancellationToken cancellationToken = default);

    Task<Result> SetConditionGradeActiveAsync(
        string adminUserId, Guid conditionGradeId, bool isActive, CancellationToken cancellationToken = default);

    // ---- Discount reasons ----
    Task<Result<Guid>> CreateDiscountReasonAsync(
        string adminUserId, string code, string name, string? description, CancellationToken cancellationToken = default);

    Task<Result> UpdateDiscountReasonAsync(
        string adminUserId, Guid discountReasonId, string name, string? description, CancellationToken cancellationToken = default);

    Task<Result> SetDiscountReasonActiveAsync(
        string adminUserId, Guid discountReasonId, bool isActive, CancellationToken cancellationToken = default);

    // ---- Brands ----
    Task<Result<Guid>> CreateBrandAsync(
        string adminUserId, string name, CancellationToken cancellationToken = default);

    Task<Result> RenameBrandAsync(
        string adminUserId, Guid brandId, string name, CancellationToken cancellationToken = default);

    Task<Result> SetBrandActiveAsync(
        string adminUserId, Guid brandId, bool isActive, CancellationToken cancellationToken = default);
}
