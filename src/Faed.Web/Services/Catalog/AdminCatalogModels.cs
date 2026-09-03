namespace Faed.Web.Services.Catalog;

// ---- Read models -----------------------------------------------------------------

public sealed record CategoryNodeView(
    Guid Id,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    int SortOrder,
    bool IsActive,
    int Depth,
    int ListingCount);

public sealed record ConditionGradeView(
    Guid Id, string Code, string Name, string Description, int SortOrder, bool IsActive, int ListingCount);

public sealed record DiscountReasonView(
    Guid Id, string Code, string Name, string? Description, bool IsActive, int ListingCount);

public sealed record BrandView(Guid Id, string Name, string Slug, bool IsActive, int ListingCount);

/// <summary>Everything the admin catalog screen needs in one round trip.</summary>
public sealed record AdminCatalogOverview(
    IReadOnlyList<CategoryNodeView> Categories,
    IReadOnlyList<ConditionGradeView> ConditionGrades,
    IReadOnlyList<DiscountReasonView> DiscountReasons,
    IReadOnlyList<BrandView> Brands);
