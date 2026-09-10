using Faed.Web.Data.Seed;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Marketplace;

/// <inheritdoc />
public sealed class PublicMarketplaceService(
    IApplicationDbContext db, IMerchantResponseService responses) : IPublicMarketplaceService
{
    public async Task<HomePageView> GetHomePageAsync(CancellationToken cancellationToken = default)
    {
        var launchCategoryIds = await GetLaunchSectorCategoryIdsAsync(cancellationToken);

        var featuredIds = await PublicLiveListings()
            .Where(l => launchCategoryIds.Contains(l.CategoryId))
            .OrderByDescending(l => l.PublishedAtUtc)
            .Take(8)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var featured = await HydrateCardsAsync(featuredIds, cancellationToken);

        // Only the launch sector's own categories are ever shown — a category added under a
        // future sector must not appear in the MVP UI just because it is active
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId != null && launchCategoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Slug, c.Name })
            .ToListAsync(cancellationToken);

        var categoryCounts = await PublicLiveListings()
            .Where(l => launchCategoryIds.Contains(l.CategoryId))
            .GroupBy(l => l.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, cancellationToken);

        var categoryNav = categories
            .Select(c => new CategoryNavItem(c.Slug, c.Name, categoryCounts.GetValueOrDefault(c.Id)))
            .ToList();

        var liveListingCount = await PublicLiveListings()
            .Where(l => launchCategoryIds.Contains(l.CategoryId))
            .CountAsync(cancellationToken);

        var verifiedMerchantCount = await db.MerchantProfiles
            .AsNoTracking()
            .CountAsync(m => m.VerificationStatus == MerchantVerificationStatus.Approved, cancellationToken);

        return new HomePageView(featured, categoryNav, liveListingCount, verifiedMerchantCount);
    }

    public async Task<ShopResultView> BrowseListingsAsync(ShopQuery query, CancellationToken cancellationToken = default)
    {
        // Every slug/code filter must resolve to zero results when it does not match anything
        // real, rather than being silently dropped — otherwise "?merchant=does-not-exist" would
        // return the whole marketplace instead of nothing.
        var unresolved = false;
        var launchCategoryIds = await GetLaunchSectorCategoryIdsAsync(cancellationToken);

        Guid? merchantId = null;
        if (!string.IsNullOrWhiteSpace(query.MerchantSlug))
        {
            merchantId = await db.MerchantProfiles
                .AsNoTracking()
                .Where(m => m.PublicSlug == query.MerchantSlug && m.VerificationStatus == MerchantVerificationStatus.Approved)
                .Select(m => (Guid?)m.Id)
                .FirstOrDefaultAsync(cancellationToken);
            unresolved |= merchantId is null;
        }

        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            categoryId = await db.Categories
                .AsNoTracking()
                .Where(c => c.Slug == query.CategorySlug && c.IsActive)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // A category outside the launch sector (present or hypothetical) is treated the
            // same as one that does not exist at all — never browsable in the MVP UI.
            unresolved |= categoryId is null || !launchCategoryIds.Contains(categoryId.Value);
        }

        Guid? gradeId = null;
        if (!string.IsNullOrWhiteSpace(query.ConditionCode))
        {
            gradeId = await db.ConditionGrades
                .AsNoTracking()
                .Where(g => g.Code == query.ConditionCode && g.IsActive)
                .Select(g => (Guid?)g.Id)
                .FirstOrDefaultAsync(cancellationToken);
            unresolved |= gradeId is null;
        }

        var facets = await GetFacetsAsync(launchCategoryIds, cancellationToken);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? ShopQuery.DefaultPageSize : query.PageSize, 1, ShopQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);

        if (unresolved)
        {
            return new ShopResultView([], 0, page, pageSize, facets, query with { Page = page, PageSize = pageSize });
        }

        var baseQuery = PublicLiveListings().Where(l => launchCategoryIds.Contains(l.CategoryId));

        if (merchantId is { } mid)
        {
            baseQuery = baseQuery.Where(l => l.MerchantProfileId == mid);
        }

        if (categoryId is { } cid)
        {
            baseQuery = baseQuery.Where(l => l.CategoryId == cid);
        }

        if (gradeId is { } gid)
        {
            baseQuery = baseQuery.Where(l => l.ConditionGradeId == gid);
        }

        if (query.MinPrice is { } min)
        {
            baseQuery = baseQuery.Where(l => l.RetailPrice >= min);
        }

        if (query.MaxPrice is { } max)
        {
            baseQuery = baseQuery.Where(l => l.RetailPrice <= max);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = query.SearchText.Trim();
            baseQuery = baseQuery.Where(l =>
                EF.Functions.Like(l.Title, $"%{term}%")
                || (l.Description != null && EF.Functions.Like(l.Description, $"%{term}%")));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // Cap page to the real last page once the true count is known: an out-of-range page
        // number (typed into the URL, or stale after the result set shrank) must show the last
        // real page of results, never an empty page with a positive total and no way back
        // (this also keeps (page - 1) * pageSize bounded by real data, never by an
        // attacker-supplied page number).
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Min(page, totalPages);
        var normalizedQuery = query with { Page = page, PageSize = pageSize };

        // A merchant who ignores reservations ranks below responsive merchants, whatever the
        // buyer's chosen sort (docs/BUSINESS-MODEL.md §8.4). New sellers are never in this set,
        // so the penalty only lands on a merchant with a real, poor track record.
        var lowResponders = (await responses.GetLowResponderMerchantIdsAsync(cancellationToken)).ToArray();
        IOrderedQueryable<Listing> ordered = baseQuery.OrderBy(l => lowResponders.Contains(l.MerchantProfileId));

        // Every sort ends on l.Id — a unique, stable final key. Without it, listings that tie on
        // price and publication timestamp have no defined order, so they can swap places between
        // requests and appear twice (or not at all) as the reader pages through.
        ordered = query.Sort switch
        {
            ShopSort.PriceLowToHigh => ordered
                .ThenBy(l => l.RetailPrice ?? decimal.MaxValue)
                .ThenByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
            ShopSort.PriceHighToLow => ordered
                .ThenByDescending(l => l.RetailPrice ?? decimal.MinValue)
                .ThenByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
            _ => ordered
                .ThenByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
        };
        baseQuery = ordered;

        var pageIds = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var items = await HydrateCardsAsync(pageIds, cancellationToken);

        return new ShopResultView(items, totalCount, page, pageSize, facets, normalizedQuery);
    }

    public async Task<PublicListingDetailView?> GetListingBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // The launch-sector boundary is a public-visibility rule, not just a browse filter: a
        // Live listing filed under a category outside Open-Box & Ex-Display must 404 on its own
        // slug exactly as it is absent from Home and Shop, or direct URL access would be a
        // hole straight through the "Do not expose unrelated sectors in the MVP UI" rule
        var launchCategoryIds = await GetLaunchSectorCategoryIdsAsync(cancellationToken);

        var listing = await PublicLiveListings()
            .Where(l => launchCategoryIds.Contains(l.CategoryId))
            .AsSplitQuery()
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .Include(l => l.Media)
            .Include(l => l.DiscountReasons)
            .Where(l => l.Slug == slug)
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return null;
        }

        var merchant = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == listing.MerchantProfileId)
            .Select(m => new { m.BusinessName, m.PublicSlug })
            .SingleAsync(cancellationToken);

        var response = await responses.GetAsync(listing.MerchantProfileId, cancellationToken);

        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == listing.CategoryId)
            .Select(c => new { c.Name, c.Slug })
            .SingleAsync(cancellationToken);

        var grade = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => g.Id == listing.ConditionGradeId)
            .Select(g => new { g.Code, g.Name, g.Description })
            .SingleAsync(cancellationToken);

        var reasonIds = listing.DiscountReasons.Select(r => r.DiscountReasonId).ToList();
        var reasonNames = await db.DiscountReasons
            .AsNoTracking()
            .Where(r => reasonIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var optionNameByValueId = listing.Options
            .SelectMany(o => o.Values.Select(v => new { v.Id, OptionName = o.Name, v.Value }))
            .ToDictionary(x => x.Id, x => (x.OptionName, x.Value));

        return new PublicListingDetailView(
            listing.Id,
            listing.Title,
            listing.Slug,
            listing.Description,
            category.Name,
            category.Slug,
            grade.Code,
            grade.Name,
            grade.Description,
            listing.ReferencePrice,
            listing.RetailPrice,
            listing.ReturnPolicyText,
            listing.WarrantyType,
            listing.WarrantyMonths,
            listing.IncludedItemsText,
            listing.MissingItemsText,
            reasonNames,
            [.. listing.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new ListingOptionView(
                    o.Id,
                    o.Name,
                    [.. o.Values.OrderBy(v => v.SortOrder).Select(v => new ListingOptionValueView(v.Id, v.Value))]))],
            [.. listing.Variants
                .OrderBy(v => v.Sku)
                .Select(v => new PublicListingVariantView(
                    v.Id,
                    ListingQueries.DescribeOptions(v, optionNameByValueId),
                    v.AvailableQuantity,
                    v.IsActive))],
            [.. listing.Media
                .OrderBy(m => m.MediaType)
                .ThenBy(m => m.SortOrder)
                .Select(m => new ListingImageView(
                    m.Id, m.MediaType, m.AltText, m.OriginalFileName, m.SizeBytes, m.SortOrder))],
            listing.MerchantProfileId,
            merchant.BusinessName,
            merchant.PublicSlug,
            // Reachable only via PublicLiveListings(), which already requires the owning
            // merchant to be Approved — true unconditionally here, not re-derived.
            MerchantIsVerified: true,
            response,
            listing.PublishedAtUtc ?? listing.UpdatedAtUtc);
    }

    public async Task<PublicMerchantProfileView?> GetMerchantStoreHeaderBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var merchant = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.PublicSlug == slug && m.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(m => new { m.Id, m.BusinessName, m.PublicSlug, m.CreatedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);

        if (merchant is null)
        {
            return null;
        }

        var liveCount = await db.Listings
            .AsNoTracking()
            .CountAsync(l => l.MerchantProfileId == merchant.Id && l.Status == ListingStatus.Live, cancellationToken);

        var response = await responses.GetAsync(merchant.Id, cancellationToken);

        return new PublicMerchantProfileView(
            merchant.Id, merchant.BusinessName, merchant.PublicSlug, true, merchant.CreatedAtUtc, liveCount, response);
    }

    /// <summary>
    /// The public-visibility gate every browse/detail query shares: <c>Live</c>
    /// and the owning merchant still <c>Approved</c>
    /// — a merchant
    /// suspended after publishing must disappear from the public marketplace even though
    /// their listings keep their own Live status untouched.
    /// </summary>
    private IQueryable<Listing> PublicLiveListings() =>
        db.Listings
            .AsNoTracking()
            .Where(l => l.Status == ListingStatus.Live
                && db.MerchantProfiles.Any(m =>
                    m.Id == l.MerchantProfileId && m.VerificationStatus == MerchantVerificationStatus.Approved));

    /// <summary>
    /// Every active category id inside the <c>Open-Box &amp; Ex-Display</c> launch sector, walked from
    /// its root (<see cref="CatalogDataSeeder.RootCategorySlug"/>) — the boundary that keeps a
    /// category added under a future sector from appearing in the MVP UI just because it is
    /// active. The table is small and admin-managed, so one full read
    /// per request is simple and sufficient — no caching before a real bottleneck is measured
    /// </summary>
    private async Task<IReadOnlySet<Guid>> GetLaunchSectorCategoryIdsAsync(CancellationToken cancellationToken)
    {
        var all = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.ParentCategoryId, c.Slug })
            .ToListAsync(cancellationToken);

        // Case-insensitively, exactly as the seeder matches it (CatalogDataSeeder): the DB does
        // the lookup under a case-insensitive collation, but this comparison runs in memory
        // after materialization, where "==" is ordinal — an existing root whose casing differs
        // from the constant would otherwise be missed and make Home and Shop appear empty.
        var root = all.FirstOrDefault(c =>
            string.Equals(c.Slug, CatalogDataSeeder.RootCategorySlug, StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            return new HashSet<Guid>();
        }

        var descendantIds = new HashSet<Guid>();
        var frontier = new Queue<Guid>();
        frontier.Enqueue(root.Id);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var child in all.Where(c => c.ParentCategoryId == current))
            {
                if (descendantIds.Add(child.Id))
                {
                    frontier.Enqueue(child.Id);
                }
            }
        }

        return descendantIds;
    }

    /// <summary>
    /// The DB-driven filter choices: the full admin-managed reference lists of condition
    /// grades and of the launch sector's categories, both small.
    /// </summary>
    private async Task<ShopFacets> GetFacetsAsync(
        IReadOnlySet<Guid> launchCategoryIds, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId != null && launchCategoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new FacetOption(c.Slug, c.Name))
            .ToListAsync(cancellationToken);

        var conditions = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .Select(g => new FacetOption(g.Code, $"Grade {g.Code} — {g.Name}"))
            .ToListAsync(cancellationToken);

        return new ShopFacets(categories, conditions);
    }

    /// <summary>
    /// Loads full card data for a bounded, already-paged set of listing ids and returns them in
    /// the same order. Splitting browse into "find the page of ids" then "hydrate those rows"
    /// keeps the filter/sort query simple to translate while still touching each reference
    /// table only once per call, not once per row. The hydration
    /// load re-applies <see cref="PublicLiveListings"/> rather than trusting the id list: a
    /// listing hidden by moderation, or whose merchant is suspended, in the gap between "find
    /// the page of ids" and "hydrate those rows" is dropped here instead of rendered as a
    /// public card.
    /// </summary>
    private async Task<IReadOnlyList<ListingCardView>> HydrateCardsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var listings = await PublicLiveListings()
            .Where(l => ids.Contains(l.Id))
            .AsSplitQuery()
            .Include(l => l.Media)
            .Include(l => l.DiscountReasons)
            .Include(l => l.Variants)
            .ToListAsync(cancellationToken);

        var merchantIds = listings.Select(l => l.MerchantProfileId).Distinct().ToList();
        var merchants = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => merchantIds.Contains(m.Id))
            .ToDictionaryAsync(
                m => m.Id,
                m => (m.BusinessName, m.PublicSlug, IsVerified: m.VerificationStatus == MerchantVerificationStatus.Approved),
                cancellationToken);

        var categoryIds = listings.Select(l => l.CategoryId).Distinct().ToList();
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var gradeIds = listings.Select(l => l.ConditionGradeId).Distinct().ToList();
        var grades = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => gradeIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => (g.Code, g.Name), cancellationToken);

        var reasonIds = listings.SelectMany(l => l.DiscountReasons.Select(dr => dr.DiscountReasonId)).Distinct().ToList();
        var reasons = reasonIds.Count == 0
            ? new Dictionary<Guid, DiscountReason>()
            : await db.DiscountReasons
                .AsNoTracking()
                .Where(r => reasonIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

        var byId = listings.ToDictionary(l => l.Id);

        return [.. ids
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Select(l => BuildCard(l, merchants[l.MerchantProfileId], categories[l.CategoryId], grades[l.ConditionGradeId], reasons))];
    }

    private static ListingCardView BuildCard(
        Listing listing,
        (string BusinessName, string PublicSlug, bool IsVerified) merchant,
        string categoryName,
        (string Code, string Name) grade,
        IReadOnlyDictionary<Guid, DiscountReason> reasonsById)
    {
        var reasonNames = listing.DiscountReasons
            .Select(dr => reasonsById.TryGetValue(dr.DiscountReasonId, out var reason) ? reason.Name : null)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var primaryImage = listing.Media
            .Where(m => m.MediaType == ListingMediaType.Product)
            .OrderBy(m => m.SortOrder)
            .FirstOrDefault();

        return new ListingCardView(
            listing.Id,
            listing.Title,
            listing.Slug,
            merchant.BusinessName,
            merchant.PublicSlug,
            merchant.IsVerified,
            categoryName,
            grade.Code,
            grade.Name,
            listing.RetailPrice,
            listing.ReferencePrice,
            listing.Variants.Where(v => v.IsActive).Sum(v => v.AvailableQuantity),
            primaryImage?.Id,
            primaryImage?.AltText,
            reasonNames.FirstOrDefault(),
            reasonNames.Count);
    }
}
