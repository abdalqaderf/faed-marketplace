using Faed.Web.Data.Seed;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Listings;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Marketplace;

/// <inheritdoc />
public sealed class PublicMarketplaceService(IApplicationDbContext db) : IPublicMarketplaceService
{
    // Generic listing options are merchant-authored per listing (docs/04-DOMAIN-MODEL.md §4);
    // there is no shared "Size"/"Colour" reference table to filter against. These are the
    // names the seeded launch categories' merchants are expected to use
    // (docs/07-UI-UX-SPEC.md §4 "Shop" filters: size, color) — matched case-insensitively via
    // the database's default collation, same as every other catalog lookup in this service.
    private static readonly string[] SizeOptionNames = ["Size"];
    private static readonly string[] ColorOptionNames = ["Colour", "Color"];

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
        // (AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI").
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
        // return the whole marketplace instead of nothing (docs/06-ARCHITECTURE.md §12).
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

        Guid? reasonId = null;
        if (!string.IsNullOrWhiteSpace(query.DiscountReasonCode))
        {
            reasonId = await db.DiscountReasons
                .AsNoTracking()
                .Where(r => r.Code == query.DiscountReasonCode && r.IsActive)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);
            unresolved |= reasonId is null;
        }

        Guid? brandId = null;
        if (!string.IsNullOrWhiteSpace(query.BrandSlug))
        {
            brandId = await db.Brands
                .AsNoTracking()
                .Where(b => b.Slug == query.BrandSlug && b.IsActive)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(cancellationToken);
            unresolved |= brandId is null;
        }

        var facets = await GetFacetsAsync(merchantId, launchCategoryIds, cancellationToken);
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

        if (reasonId is { } rid)
        {
            baseQuery = baseQuery.Where(l => l.DiscountReasons.Any(dr => dr.DiscountReasonId == rid));
        }

        if (brandId is { } bid)
        {
            baseQuery = baseQuery.Where(l => l.BrandId == bid);
        }

        // Variant-aware, not listing-aware: a size/colour filter must be satisfied by a single
        // sellable variant (active, in stock) that carries the requested value(s) together — not
        // merely by the values existing somewhere on the listing's option lists. Otherwise a
        // "Red" + "XL" filter would match a listing that only stocks Red/M and Blue/XL, whose
        // requested SKU cannot actually be bought (faed-commerce-ux "do not imply stock exists
        // at listing level when the selected SKU is unavailable").
        if (!string.IsNullOrWhiteSpace(query.SizeValue) || !string.IsNullOrWhiteSpace(query.ColorValue))
        {
            var size = string.IsNullOrWhiteSpace(query.SizeValue) ? null : query.SizeValue;
            var color = string.IsNullOrWhiteSpace(query.ColorValue) ? null : query.ColorValue;
            baseQuery = baseQuery.Where(l => l.Variants.Any(v =>
                v.IsActive && v.AvailableQuantity > 0
                && (size == null || v.OptionValues.Any(ov =>
                    SizeOptionNames.Contains(ov.OptionValue.Option.Name) && ov.OptionValue.Value == size))
                && (color == null || v.OptionValues.Any(ov =>
                    ColorOptionNames.Contains(ov.OptionValue.Option.Name) && ov.OptionValue.Value == color))));
        }

        // A B2B-only listing has no RetailPrice at all; fall back to the wholesale indicative
        // price so it is neither invisible to a price filter nor mis-sorted as free/priceless
        // (docs/04-DOMAIN-MODEL.md §3).
        if (query.MinPrice is { } min)
        {
            baseQuery = baseQuery.Where(l => (l.RetailPrice ?? l.WholesaleIndicativeUnitPrice) >= min);
        }

        if (query.MaxPrice is { } max)
        {
            baseQuery = baseQuery.Where(l => (l.RetailPrice ?? l.WholesaleIndicativeUnitPrice) <= max);
        }

        // Inclusive by design: a buyer filtering for "retail available" wants everything they
        // can buy at retail, including a listing that also happens to support wholesale — the
        // filter is not "retail exclusively" (faed-commerce-ux "B2C/B2B availability" is
        // informational, not mutually exclusive). The UI labels this accurately rather than
        // implying exclusivity.
        baseQuery = query.Channel switch
        {
            MarketplaceChannel.RetailOnly => baseQuery.Where(l => l.AllowB2C),
            MarketplaceChannel.WholesaleOnly => baseQuery.Where(l => l.AllowB2B),
            _ => baseQuery,
        };

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = query.SearchText.Trim();
            baseQuery = baseQuery.Where(l => EF.Functions.Like(l.Title, $"%{term}%") || EF.Functions.Like(l.Description, $"%{term}%"));
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

        // Every sort ends on l.Id — a unique, stable final key. Without it, listings that tie on
        // price and publication timestamp have no defined order, so they can swap places between
        // requests and appear twice (or not at all) as the reader pages through.
        baseQuery = query.Sort switch
        {
            ShopSort.PriceLowToHigh => baseQuery
                .OrderBy(l => l.RetailPrice ?? l.WholesaleIndicativeUnitPrice ?? decimal.MaxValue)
                .ThenByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
            ShopSort.PriceHighToLow => baseQuery
                .OrderByDescending(l => l.RetailPrice ?? l.WholesaleIndicativeUnitPrice ?? decimal.MinValue)
                .ThenByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
            _ => baseQuery
                .OrderByDescending(l => l.PublishedAtUtc)
                .ThenBy(l => l.Id),
        };

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
        // Live listing filed under a category outside Fashion Overstock must 404 on its own
        // slug exactly as it is absent from Home and Shop, or direct URL access would be a
        // hole straight through the "Do not expose unrelated sectors in the MVP UI" rule
        // (AGENTS.md §3, docs/14-FUTURE-EXPANSION.md).
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
            brandName,
            grade.Code,
            grade.Name,
            grade.Description,
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

        return new PublicMerchantProfileView(
            merchant.Id, merchant.BusinessName, merchant.PublicSlug, true, merchant.CreatedAtUtc, liveCount);
    }

    /// <summary>
    /// The public-visibility gate every browse/detail query shares: <c>Live</c>
    /// (docs/03-BUSINESS-RULES.md §2) and the owning merchant still <c>Approved</c>
    /// (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved") — a merchant
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
    /// Every active category id inside the <c>Fashion Overstock</c> launch sector, walked from
    /// its root (<see cref="CatalogDataSeeder.RootCategorySlug"/>) — the boundary that keeps a
    /// category added under a future sector from appearing in the MVP UI just because it is
    /// active (AGENTS.md §3 "Do not expose unrelated sectors in the MVP UI",
    /// docs/14-FUTURE-EXPANSION.md). The table is small and admin-managed, so one full read
    /// per request is simple and sufficient — no caching before a real bottleneck is measured
    /// (docs/06-ARCHITECTURE.md §13).
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
    /// The DB-driven filter choices. Categories/conditions/reasons are the full admin-managed
    /// reference lists, restricted to the launch sector for categories
    /// (tasks/TASK-003-CATALOG.md); brands, sizes and colours are the uncontrolled exceptions,
    /// so only values actually used by a matching Live listing are offered
    /// (docs/04-DOMAIN-MODEL.md §2 "Brand is optional", §4 generic options).
    /// </summary>
    private async Task<ShopFacets> GetFacetsAsync(
        Guid? merchantId, IReadOnlySet<Guid> launchCategoryIds, CancellationToken cancellationToken)
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

        var reasons = await db.DiscountReasons
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new FacetOption(r.Code, r.Name))
            .ToListAsync(cancellationToken);

        var scopedListings = PublicLiveListings().Where(l => launchCategoryIds.Contains(l.CategoryId));
        if (merchantId is { } mid)
        {
            scopedListings = scopedListings.Where(l => l.MerchantProfileId == mid);
        }

        var brandIds = await scopedListings
            .Where(l => l.BrandId != null)
            .Select(l => l.BrandId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var brands = brandIds.Count == 0
            ? []
            : await db.Brands
                .AsNoTracking()
                .Where(b => brandIds.Contains(b.Id) && b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new FacetOption(b.Slug, b.Name))
                .ToListAsync(cancellationToken);

        var (sizes, colors) = await GetSizeAndColorFacetsAsync(scopedListings, cancellationToken);

        return new ShopFacets(categories, conditions, reasons, brands, sizes, colors);
    }

    private static async Task<(IReadOnlyList<FacetOption> Sizes, IReadOnlyList<FacetOption> Colors)> GetSizeAndColorFacetsAsync(
        IQueryable<Listing> scopedListings, CancellationToken cancellationToken)
    {
        // Project the distinct (option name, value) pairs in the database rather than pulling
        // every matching listing and its whole option graph into memory: the facet list is
        // bounded by the vocabulary merchants actually use, not by catalog size. Only values on
        // a sellable variant (active, in stock) are offered, so the filter never presents a
        // choice that resolves to nothing (matches the variant-aware filter above).
        var flattened = await scopedListings
            .SelectMany(l => l.Variants)
            .Where(v => v.IsActive && v.AvailableQuantity > 0)
            .SelectMany(v => v.OptionValues)
            .Select(ov => new { OptionName = ov.OptionValue.Option.Name, ov.OptionValue.Value })
            .Distinct()
            .ToListAsync(cancellationToken);

        var sizes = flattened
            .Where(x => SizeOptionNames.Contains(x.OptionName, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(v => new FacetOption(v, v))
            .ToList();

        var colors = flattened
            .Where(x => ColorOptionNames.Contains(x.OptionName, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(v => new FacetOption(v, v))
            .ToList();

        return (sizes, colors);
    }

    /// <summary>
    /// Loads full card data for a bounded, already-paged set of listing ids and returns them in
    /// the same order. Splitting browse into "find the page of ids" then "hydrate those rows"
    /// keeps the filter/sort query simple to translate while still touching each reference
    /// table only once per call, not once per row (docs/06-ARCHITECTURE.md §13). The hydration
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
            listing.AllowB2C,
            listing.AllowB2B,
            listing.WholesaleIndicativeUnitPrice,
            listing.WholesaleMinQuantity,
            listing.Variants.Where(v => v.IsActive).Sum(v => v.AvailableQuantity),
            primaryImage?.Id,
            primaryImage?.AltText,
            reasonNames.FirstOrDefault(),
            reasonNames.Count);
    }
}
