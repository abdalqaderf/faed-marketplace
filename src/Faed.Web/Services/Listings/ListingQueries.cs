using Faed.Web.Models.Entities;
using Faed.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Listings;

/// <summary>
/// Shared loading and projection for the listing aggregate. The merchant workspace and the
/// admin review screen deliberately render the same <see cref="ListingDetailView"/>: an
/// admin must judge exactly what the merchant submitted, so there is one projection rather
/// than two that can drift apart.
/// </summary>
internal static class ListingQueries
{
    /// <summary>The whole aggregate, tracked, for a use case that is about to change it.</summary>
    internal static IQueryable<Listing> WithAggregate(this IQueryable<Listing> listings) =>
        listings
            .AsSplitQuery()
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .Include(l => l.Media)
            .Include(l => l.DiscountReasons)
            .Include(l => l.ReferencePriceEvidence)
            .Include(l => l.Moderations);

    /// <summary>
    /// Builds the display projection, resolving the catalog and merchant names the listing
    /// only stores as foreign keys. Names come from the database, never from constants
    /// (tasks/TASK-003-CATALOG.md "no catalog values hard-coded").
    /// </summary>
    internal static async Task<ListingDetailView> ToDetailViewAsync(
        this Listing listing,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var merchantName = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == listing.MerchantProfileId)
            .Select(m => m.BusinessName)
            .SingleOrDefaultAsync(cancellationToken) ?? "Unknown merchant";

        var categoryName = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == listing.CategoryId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "Uncategorised";

        var grade = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => g.Id == listing.ConditionGradeId)
            .Select(g => new { g.Code, g.Name, g.Description })
            .SingleOrDefaultAsync(cancellationToken);

        string? brandName = null;
        if (listing.BrandId is { } brandId)
        {
            brandName = await db.Brands
                .AsNoTracking()
                .Where(b => b.Id == brandId)
                .Select(b => b.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var reasonIds = listing.DiscountReasons.Select(r => r.DiscountReasonId).ToList();
        var reasonNames = await db.DiscountReasons
            .AsNoTracking()
            .Where(r => reasonIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);
        // Codes are the stable natural key (docs/13-CATALOG seed); prefer them over Name for
        // any logic branch, since an admin renaming a reason's display text must not silently
        // change behaviour that depends on which reason is selected.
        var reasonCodes = await db.DiscountReasons
            .AsNoTracking()
            .Where(r => reasonIds.Contains(r.Id))
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);

        var optionNameByValueId = listing.Options
            .SelectMany(o => o.Values.Select(v => new { v.Id, OptionName = o.Name, v.Value }))
            .ToDictionary(x => x.Id, x => (x.OptionName, x.Value));

        return new ListingDetailView(
            listing.Id,
            listing.MerchantProfileId,
            merchantName,
            listing.Title,
            listing.Slug,
            listing.Description,
            listing.CategoryId,
            categoryName,
            listing.BrandId,
            brandName,
            listing.ConditionGradeId,
            grade?.Code ?? "?",
            grade?.Name ?? "Unknown condition",
            grade?.Description ?? string.Empty,
            listing.ReferencePrice,
            listing.RetailPrice,
            listing.WholesaleIndicativeUnitPrice,
            listing.WholesaleMinQuantity,
            listing.AllowB2C,
            listing.AllowB2B,
            listing.AllowMixedVariantB2B,
            listing.ReturnPolicyText,
            listing.WarrantyText,
            listing.IncludedItemsText,
            listing.MissingItemsText,
            listing.Status,
            listing.HiddenByAdmin,
            listing.SubmittedAtUtc,
            listing.PublishedAtUtc,
            listing.UpdatedAtUtc,
            listing.AcceptsMaterialEdit,
            reasonNames,
            reasonCodes,
            reasonIds,
            [.. listing.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new ListingOptionView(
                    o.Id,
                    o.Name,
                    [.. o.Values.OrderBy(v => v.SortOrder).Select(v => new ListingOptionValueView(v.Id, v.Value))]))],
            [.. listing.Variants
                .OrderBy(v => v.Sku)
                .Select(v => new ListingVariantView(
                    v.Id,
                    v.Sku,
                    DescribeOptions(v, optionNameByValueId),
                    v.AvailableQuantity,
                    v.ReservedQuantity,
                    v.SoldQuantity,
                    v.IsActive))],
            [.. listing.Media
                .OrderBy(m => m.MediaType)
                .ThenBy(m => m.SortOrder)
                .Select(m => new ListingImageView(
                    m.Id, m.MediaType, m.AltText, m.OriginalFileName, m.SizeBytes, m.SortOrder))],
            [.. listing.ReferencePriceEvidence
                .OrderBy(e => e.CreatedAtUtc)
                .Select(e => new ListingEvidenceView(
                    e.Id,
                    e.EvidenceType,
                    e.ReferenceUrl,
                    e.Note,
                    e.OriginalFileName,
                    e.StorageObjectKey is not null,
                    e.CreatedAtUtc))],
            [.. listing.Moderations
                .OrderByDescending(m => m.SubmittedAtUtc)
                .Select(m => new ListingModerationView(
                    m.Id, m.Status, m.ReasonForReview, m.ReviewNote, m.SubmittedAtUtc, m.ReviewedAtUtc))],
            listing.DescribeSubmissionBlockers(grade?.Code ?? "?", reasonCodes));
    }

    /// <summary>
    /// Resolves the stable catalog codes that <see cref="Listing.DescribeSubmissionBlockers"/>,
    /// <see cref="Listing.SubmitForReview"/>, <see cref="Listing.RemoveMedia"/> and
    /// <see cref="Listing.DisclosesAPhysicalImperfection"/> need to decide whether a physical
    /// imperfection must be photographed — the aggregate stores only the catalog ids
    /// (docs/03-BUSINESS-RULES.md §3, "defects must be disclosed and visually evidenced").
    /// </summary>
    internal static async Task<(string ConditionGradeCode, IReadOnlyCollection<string> DiscountReasonCodes)>
        LoadDisclosureCodesAsync(this Listing listing, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var conditionGradeCode = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => g.Id == listing.ConditionGradeId)
            .Select(g => g.Code)
            .SingleAsync(cancellationToken);

        var reasonIds = listing.DiscountReasons.Select(r => r.DiscountReasonId).ToList();
        var discountReasonCodes = reasonIds.Count == 0
            ? []
            : await db.DiscountReasons
                .AsNoTracking()
                .Where(r => reasonIds.Contains(r.Id))
                .Select(r => r.Code)
                .ToListAsync(cancellationToken);

        return (conditionGradeCode, discountReasonCodes);
    }

    internal static IReadOnlyList<VariantOptionView> DescribeOptions(
        ListingVariant variant,
        IReadOnlyDictionary<Guid, (string OptionName, string Value)> optionNameByValueId) =>
        [.. variant.OptionValues
            .Select(ov => optionNameByValueId.TryGetValue(ov.ListingOptionValueId, out var pair)
                ? new VariantOptionView(pair.OptionName, pair.Value)
                : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .OrderBy(v => v.Option, StringComparer.Ordinal)];
}
