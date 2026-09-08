using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Catalog;

/// <inheritdoc />
public sealed class AdminCatalogService(
    IApplicationDbContext db,
    IUserRoleService userRoles,
    ILogger<AdminCatalogService> logger) : IAdminCatalogService
{
    public async Task<AdminCatalogOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: false, includeRoot: true, cancellationToken);
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => launchCategoryIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.ParentCategoryId,
                c.Name,
                c.Slug,
                c.SortOrder,
                c.IsActive,
                ListingCount = db.Listings.Count(l => l.CategoryId == c.Id),
            })
            .ToListAsync(cancellationToken);

        var childrenByParent = categories.ToLookup(c => c.ParentCategoryId);

        // Depth-first from each root so the view can render an indented tree.
        var orderedCategories = new List<CategoryNodeView>();
        void Emit(Guid? parentId, int depth)
        {
            var children = childrenByParent[parentId]
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name);

            foreach (var c in children)
            {
                orderedCategories.Add(new CategoryNodeView(
                    c.Id, c.ParentCategoryId, c.Name, c.Slug, c.SortOrder, c.IsActive, depth, c.ListingCount));
                Emit(c.Id, depth + 1);
            }
        }

        Emit(null, 0);
        // Any category orphaned by a missing parent row still gets listed.
        foreach (var c in categories.Where(c => orderedCategories.All(o => o.Id != c.Id)))
        {
            orderedCategories.Add(new CategoryNodeView(
                c.Id, c.ParentCategoryId, c.Name, c.Slug, c.SortOrder, c.IsActive, 0, c.ListingCount));
        }

        var grades = await db.ConditionGrades
            .AsNoTracking()
            .OrderBy(g => g.SortOrder)
            .Select(g => new ConditionGradeView(
                g.Id, g.Code, g.Name, g.Description, g.SortOrder, g.IsActive,
                db.Listings.Count(l => l.ConditionGradeId == g.Id)))
            .ToListAsync(cancellationToken);

        var reasons = await db.DiscountReasons
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new DiscountReasonView(
                r.Id, r.Code, r.Name, r.Description, r.IsActive,
                db.Listings.Count(l => l.DiscountReasons.Any(x => x.DiscountReasonId == r.Id))))
            .ToListAsync(cancellationToken);

        return new AdminCatalogOverview(orderedCategories, grades, reasons);
    }

    // ---- Categories ---------------------------------------------------------

    public Task<Result<Guid>> CreateCategoryAsync(
        string adminUserId, Guid? parentCategoryId, string name, int sortOrder, CancellationToken cancellationToken = default) =>
        MutateAsync<Guid>(adminUserId, async () =>
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length is 0 or > Category.MaxNameLength)
            {
                return Result<Guid>.Validation($"A category name of 1–{Category.MaxNameLength} characters is required.");
            }

            if (parentCategoryId is not { } parentId)
            {
                return Result<Guid>.Validation(
                    "Choose a parent inside Fashion Overstock. New sector roots are not available in the MVP.");
            }

            var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
                db, activeOnly: true, includeRoot: true, cancellationToken);
            if (!launchCategoryIds.Contains(parentId))
            {
                return Result<Guid>.Validation("Choose an active parent inside Fashion Overstock.");
            }

            var slug = await UniqueCategorySlugAsync(cleanName, cancellationToken);
            var category = new Category(cleanName, slug, parentCategoryId, sortOrder);
            db.Categories.Add(category);
            return Result<Guid>.Success(category.Id);
        }, cancellationToken);

    public Task<Result> UpdateCategoryAsync(
        string adminUserId, Guid categoryId, string name, int sortOrder, CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
                db, activeOnly: false, includeRoot: true, cancellationToken);
            if (!launchCategoryIds.Contains(categoryId))
            {
                return Result.NotFound("That Fashion Overstock category was not found.");
            }

            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
            if (category is null)
            {
                return Result.NotFound("That category was not found.");
            }

            try
            {
                category.UpdateDetails(name, sortOrder);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            return Result.Success();
        }, cancellationToken);

    public Task<Result> SetCategoryActiveAsync(
        string adminUserId, Guid categoryId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
                db, activeOnly: false, includeRoot: true, cancellationToken);
            if (!launchCategoryIds.Contains(categoryId))
            {
                return Result.NotFound("That Fashion Overstock category was not found.");
            }

            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
            if (category is null)
            {
                return Result.NotFound("That category was not found.");
            }

            if (!isActive && category.ParentCategoryId is null)
            {
                return Result.Validation("The sector root category cannot be deactivated.");
            }

            category.SetActive(isActive);
            return Result.Success();
        }, cancellationToken);

    // ---- Condition grades -------------------------------------------------

    public Task<Result> UpdateConditionGradeAsync(
        string adminUserId, Guid conditionGradeId, string name, string description, int sortOrder,
        CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var grade = await db.ConditionGrades.SingleOrDefaultAsync(g => g.Id == conditionGradeId, cancellationToken);
            if (grade is null)
            {
                return Result.NotFound("That condition grade was not found.");
            }

            try
            {
                grade.UpdateDetails(name, description, sortOrder);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            return Result.Success();
        }, cancellationToken);

    public Task<Result> SetConditionGradeActiveAsync(
        string adminUserId, Guid conditionGradeId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var grade = await db.ConditionGrades.SingleOrDefaultAsync(g => g.Id == conditionGradeId, cancellationToken);
            if (grade is null)
            {
                return Result.NotFound("That condition grade was not found.");
            }

            grade.SetActive(isActive);
            return Result.Success();
        }, cancellationToken);

    // ---- Discount reasons -----------------------------------------------

    public Task<Result<Guid>> CreateDiscountReasonAsync(
        string adminUserId, string code, string name, string? description, CancellationToken cancellationToken = default) =>
        MutateAsync<Guid>(adminUserId, async () =>
        {
            var cleanCode = (code ?? string.Empty).Trim();
            if (cleanCode.Length is 0 or > DiscountReason.MaxCodeLength || cleanCode.Any(char.IsWhiteSpace))
            {
                return Result<Guid>.Validation(
                    $"A single-word code of 1–{DiscountReason.MaxCodeLength} characters is required.");
            }

            if (await db.DiscountReasons.AsNoTracking().AnyAsync(r => r.Code == cleanCode, cancellationToken))
            {
                return Result<Guid>.Conflict($"A discount reason with code \"{cleanCode}\" already exists.");
            }

            DiscountReason reason;
            try
            {
                reason = new DiscountReason(cleanCode, name, description);
            }
            catch (DomainException ex)
            {
                return Result<Guid>.Validation(ex.Message);
            }

            db.DiscountReasons.Add(reason);
            return Result<Guid>.Success(reason.Id);
        }, cancellationToken);

    public Task<Result> UpdateDiscountReasonAsync(
        string adminUserId, Guid discountReasonId, string name, string? description, CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var reason = await db.DiscountReasons.SingleOrDefaultAsync(r => r.Id == discountReasonId, cancellationToken);
            if (reason is null)
            {
                return Result.NotFound("That discount reason was not found.");
            }

            try
            {
                reason.UpdateDetails(name, description);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            return Result.Success();
        }, cancellationToken);

    public Task<Result> SetDiscountReasonActiveAsync(
        string adminUserId, Guid discountReasonId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateAsync(adminUserId, async () =>
        {
            var reason = await db.DiscountReasons.SingleOrDefaultAsync(r => r.Id == discountReasonId, cancellationToken);
            if (reason is null)
            {
                return Result.NotFound("That discount reason was not found.");
            }

            reason.SetActive(isActive);
            return Result.Success();
        }, cancellationToken);

    // ---- Internals ----------------------------------------------------

    private async Task<Result> MutateAsync(
        string adminUserId, Func<Task<Result>> body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId)
            || !await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result.Forbidden();
        }

        var outcome = await body();
        if (outcome.Failed)
        {
            return outcome;
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueCatalogKeyViolation(ex))
        {
            logger.LogInformation(ex, "A concurrent catalog write collided with an existing slug or code.");
            return Result.Conflict(
                "Another catalog item already uses that slug or code. Refresh the catalog and try again.");
        }

        logger.LogInformation("Admin {AdminId} changed catalog reference data.", adminUserId);
        return outcome;
    }

    private async Task<Result<T>> MutateAsync<T>(
        string adminUserId, Func<Task<Result<T>>> body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId)
            || !await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result<T>.Forbidden();
        }

        var outcome = await body();
        if (outcome.Failed)
        {
            return outcome;
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueCatalogKeyViolation(ex))
        {
            logger.LogInformation(ex, "A concurrent catalog write collided with an existing slug or code.");
            return Result<T>.Conflict(
                "Another catalog item already uses that slug or code. Refresh the catalog and try again.");
        }

        logger.LogInformation("Admin {AdminId} changed catalog reference data.", adminUserId);
        return outcome;
    }

    private async Task<string> UniqueCategorySlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = Slug.Truncate(Slug.Create(name, "category"), Category.MaxSlugLength - 6);
        var slug = baseSlug;
        var counter = 2;
        while (await db.Categories.AsNoTracking().AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        return slug;
    }

    private static bool IsUniqueCatalogKeyViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }
}
