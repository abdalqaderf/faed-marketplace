using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.B2B;

/// <inheritdoc />
public sealed class B2BNegotiationService(
    IApplicationDbContext db,
    IClock clock,
    IUserRoleService userRoles,
    IOptions<B2BNegotiationOptions> options,
    ILogger<B2BNegotiationService> logger) : IB2BNegotiationService
{
    private readonly B2BNegotiationOptions _options = options.Value;

    // ---- Offer form -------------------------------------------------------------

    public async Task<Result<OfferListingView>> GetListingForOfferAsync(
        string merchantUserId, string listingSlug, CancellationToken cancellationToken = default)
    {
        var buyerMerchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (buyerMerchant.Failed)
        {
            return Result<OfferListingView>.From(buyerMerchant);
        }

        var listing = await LoadListingAggregateBySlugAsync(listingSlug, cancellationToken);
        if (listing is null)
        {
            return Result<OfferListingView>.NotFound("That listing is not available for wholesale offers.");
        }

        var guard = await GuardOfferListingAsync(listing, buyerMerchant.Value, cancellationToken);
        if (guard is not null)
        {
            return Result<OfferListingView>.From(guard);
        }

        var sellerName = (await LoadMerchantNamesAsync([listing.MerchantProfileId], cancellationToken))
            .GetValueOrDefault(listing.MerchantProfileId, "Merchant");
        return Result<OfferListingView>.Success(BuildOfferListingView(listing, sellerName));
    }

    // ---- Start / counter -------------------------------------------------------

    public async Task<Result<Guid>> StartNegotiationAsync(
        string merchantUserId, StartNegotiationInput input, CancellationToken cancellationToken = default)
    {
        var buyerMerchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (buyerMerchant.Failed)
        {
            return Result<Guid>.From(buyerMerchant);
        }

        var listing = await LoadListingAggregateBySlugAsync(input.ListingSlug, cancellationToken);
        if (listing is null)
        {
            return Result<Guid>.NotFound("That listing is not available for wholesale offers.");
        }

        var guard = await GuardOfferListingAsync(listing, buyerMerchant.Value, cancellationToken);
        if (guard is not null)
        {
            return Result<Guid>.From(guard);
        }

        var offer = BuildProposedOffer(input.Lines, input.ProposedUnitPrice, input.Message, input.ValidityDays, listing, out var lineError);
        if (lineError is not null)
        {
            return Result<Guid>.Validation(lineError);
        }

        B2BNegotiation negotiation;
        try
        {
            negotiation = new B2BNegotiation(
                listing.Id,
                listing.MerchantProfileId,
                buyerMerchant.Value,
                listing.WholesaleMinQuantity ?? 0,
                listing.AllowMixedVariantB2B,
                offer!,
                clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Validation(ex.Message);
        }

        db.B2BNegotiations.Add(negotiation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to open B2B negotiation for merchant {MerchantId}", buyerMerchant.Value);
            return Result<Guid>.Conflict("The offer could not be saved. Try again.");
        }

        logger.LogInformation(
            "Merchant {BuyerMerchantId} opened B2B negotiation {NegotiationId} on listing {ListingId}",
            buyerMerchant.Value, negotiation.Id, listing.Id);
        return Result<Guid>.Success(negotiation.Id);
    }

    public async Task<Result> CounterOfferAsync(
        string merchantUserId, Guid negotiationId, CounterOfferInput input, CancellationToken cancellationToken = default)
    {
        var merchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (merchant.Failed)
        {
            return Result.Forbidden(merchant.Error!);
        }

        var negotiation = await db.B2BNegotiations
            .Include(n => n.Revisions).ThenInclude(r => r.Lines)
            .SingleOrDefaultAsync(n => n.Id == negotiationId, cancellationToken);

        if (negotiation is null || !negotiation.IsParticipant(merchant.Value))
        {
            return Result.NotFound("That negotiation was not found.");
        }

        var expiry = await ExpireBeforeParticipantActionAsync(negotiation, cancellationToken);
        if (expiry is not null)
        {
            return expiry;
        }

        var listing = await LoadListingAggregateByIdAsync(negotiation.ListingId, cancellationToken);
        if (listing is null)
        {
            return Result.Conflict("The listing behind this negotiation is no longer available.");
        }

        var offer = BuildProposedOffer(input.Lines, input.ProposedUnitPrice, input.Message, input.ValidityDays, listing, out var lineError);
        if (lineError is not null)
        {
            return Result.Validation(lineError);
        }

        try
        {
            negotiation.Counter(
                merchant.Value,
                listing.WholesaleMinQuantity ?? 0,
                listing.AllowMixedVariantB2B,
                offer!,
                clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        return await SaveTransitionAsync(negotiation, "countered", cancellationToken);
    }

    // ---- Reject / cancel ----------------------------------------------------
    // Accepting the current revision lives in B2BDealService.AcceptOfferAsync: it must
    // atomically reserve stock and create the B2BDeal in the same transaction.

    public Task<Result> RejectAsync(string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default) =>
        TransitionAsync(merchantUserId, negotiationId, (n, m, now) => n.Reject(m, now), "rejected", cancellationToken);

    public Task<Result> CancelAsync(string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default) =>
        TransitionAsync(merchantUserId, negotiationId, (n, m, now) => n.Cancel(m, now), "cancelled", cancellationToken);

    private async Task<Result> TransitionAsync(
        string merchantUserId,
        Guid negotiationId,
        Action<B2BNegotiation, Guid, DateTime> transition,
        string verb,
        CancellationToken cancellationToken)
    {
        var merchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (merchant.Failed)
        {
            return Result.Forbidden(merchant.Error!);
        }

        var negotiation = await db.B2BNegotiations
            .Include(n => n.Revisions)
            .SingleOrDefaultAsync(n => n.Id == negotiationId, cancellationToken);

        if (negotiation is null || !negotiation.IsParticipant(merchant.Value))
        {
            return Result.NotFound("That negotiation was not found.");
        }

        var expiry = await ExpireBeforeParticipantActionAsync(negotiation, cancellationToken);
        if (expiry is not null)
        {
            return expiry;
        }

        try
        {
            transition(negotiation, merchant.Value, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        return await SaveTransitionAsync(negotiation, verb, cancellationToken);
    }

    private async Task<Result> SaveTransitionAsync(B2BNegotiation negotiation, string verb, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("This negotiation changed a moment ago. Reload it and try again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to persist B2B negotiation {NegotiationId} transition", negotiation.Id);
            return Result.Conflict("The change could not be saved. Reload the negotiation and try again.");
        }

        logger.LogInformation("B2B negotiation {NegotiationId} {Verb}; now {Status}", negotiation.Id, verb, negotiation.Status);
        return Result.Success();
    }

    private async Task<Result?> ExpireBeforeParticipantActionAsync(
        B2BNegotiation negotiation, CancellationToken cancellationToken)
    {
        if (!negotiation.ExpireIfLapsed(clock.UtcNow))
        {
            return null;
        }

        var saved = await SaveTransitionAsync(negotiation, "expired", cancellationToken);
        return saved.Succeeded
            ? Result.Conflict("This offer has expired and can no longer be changed.")
            : saved;
    }

    // ---- Reads ---------------------------------------------------------------

    public async Task<PagedResult<B2BNegotiationSummaryView>> GetMyNegotiationsAsync(
        string merchantUserId, B2BNegotiationFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return PagedResult<B2BNegotiationSummaryView>.Empty(page, Paging.DefaultPageSize);
        }

        // The filter is applied in SQL: "awaiting me" is an open negotiation whose current
        // revision was proposed by the other merchant.
        var id = merchantId.Value;
        var mine = ForMerchant(id);
        var filtered = filter switch
        {
            B2BNegotiationFilter.AwaitingMe => mine.Where(n => n.Status == B2BNegotiationStatus.Open
                && n.Revisions.Any(r => r.RevisionNumber == n.CurrentRevisionNumber
                    && r.ProposedByMerchantProfileId != id)),
            B2BNegotiationFilter.AwaitingThem => mine.Where(n => n.Status == B2BNegotiationStatus.Open
                && n.Revisions.Any(r => r.RevisionNumber == n.CurrentRevisionNumber
                    && r.ProposedByMerchantProfileId == id)),
            B2BNegotiationFilter.Open => mine.Where(n => n.Status == B2BNegotiationStatus.Open),
            B2BNegotiationFilter.Closed => mine.Where(n => n.Status != B2BNegotiationStatus.Open),
            _ => mine,
        };

        page = Paging.NormalizePage(page);
        var totalCount = await filtered.CountAsync(cancellationToken);
        var negotiations = await filtered
            .Include(n => n.Revisions).ThenInclude(r => r.Lines)
            .OrderByDescending(n => n.UpdatedAtUtc)
            .Skip((page - 1) * Paging.DefaultPageSize)
            .Take(Paging.DefaultPageSize)
            .ToListAsync(cancellationToken);

        var listingInfo = await LoadListingHeadersAsync(negotiations.Select(n => n.ListingId), cancellationToken);
        var merchantNames = await LoadMerchantNamesAsync(
            negotiations.SelectMany(n => new[] { n.SellingMerchantProfileId, n.BuyingMerchantProfileId }), cancellationToken);

        var views = negotiations
            .Select(n =>
            {
                var current = n.CurrentRevision;
                var header = listingInfo.GetValueOrDefault(n.ListingId);
                return new B2BNegotiationSummaryView(
                    n.Id,
                    RoleOf(n, merchantId.Value),
                    n.Status,
                    header.Title ?? "Listing",
                    header.Slug ?? string.Empty,
                    merchantNames.GetValueOrDefault(n.CounterpartyOf(merchantId.Value), "Merchant"),
                    current.RevisionNumber,
                    current.ProposedUnitPrice,
                    current.TotalQuantity,
                    current.ProposedTotal,
                    current.OfferExpiresAtUtc,
                    n.AwaitingResponseFrom == merchantId,
                    n.UpdatedAtUtc);
            })
            .ToList();

        return new PagedResult<B2BNegotiationSummaryView>(views, totalCount, page, Paging.DefaultPageSize);
    }

    public async Task<int> GetAwaitingResponseCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return 0;
        }

        var id = merchantId.Value;
        return await ForMerchant(id)
            .Where(n => n.Status == B2BNegotiationStatus.Open
                && n.Revisions.Any(r => r.RevisionNumber == n.CurrentRevisionNumber
                    && r.ProposedByMerchantProfileId != id))
            .CountAsync(cancellationToken);
    }

    private IQueryable<B2BNegotiation> ForMerchant(Guid merchantId) =>
        db.B2BNegotiations
            .AsNoTracking()
            .Where(n => n.SellingMerchantProfileId == merchantId || n.BuyingMerchantProfileId == merchantId);

    public async Task<B2BNegotiationDetailView?> GetNegotiationAsync(
        string merchantUserId, Guid negotiationId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return null;
        }

        var negotiation = await db.B2BNegotiations
            .AsNoTracking()
            .Include(n => n.Revisions).ThenInclude(r => r.Lines)
            .SingleOrDefaultAsync(n => n.Id == negotiationId, cancellationToken);

        if (negotiation is null || !negotiation.IsParticipant(merchantId.Value))
        {
            // A merchant that is not a participant learns nothing — same as "not found"
            return null;
        }

        var listing = await LoadListingAggregateByIdAsync(negotiation.ListingId, cancellationToken);
        var optionNames = listing is null
            ? new Dictionary<Guid, (string OptionName, string Value)>()
            : OptionNameByValueId(listing);

        var variantCombination = listing is null
            ? new Dictionary<Guid, string>()
            : listing.Variants.ToDictionary(v => v.Id, v => DescribeVariant(v, optionNames));

        var merchantNames = await LoadMerchantNamesAsync(
            new[] { negotiation.SellingMerchantProfileId, negotiation.BuyingMerchantProfileId }, cancellationToken);

        var revisions = negotiation.Revisions
            .OrderBy(r => r.RevisionNumber)
            .Select(r => new B2BOfferRevisionView(
                r.RevisionNumber,
                r.ProposedByMerchantProfileId == negotiation.SellingMerchantProfileId
                    ? B2BNegotiationParty.SellingMerchant
                    : B2BNegotiationParty.BuyingMerchant,
                merchantNames.GetValueOrDefault(r.ProposedByMerchantProfileId, "Merchant"),
                r.ProposedUnitPrice,
                r.TotalQuantity,
                r.ProposedTotal,
                r.Message,
                r.OfferExpiresAtUtc,
                r.CreatedAtUtc,
                r.Lines
                    .OrderBy(l => variantCombination.GetValueOrDefault(l.ListingVariantId, string.Empty), StringComparer.OrdinalIgnoreCase)
                    .Select(l => new B2BOfferLineView(
                        variantCombination.GetValueOrDefault(l.ListingVariantId, "Variant"), l.Quantity))
                    .ToList()))
            .ToList();

        // Once accepted, the negotiation is backed by exactly one B2BDeal;
        // surface its id so the UI can link straight to the fulfilment record.
        var dealId = negotiation.Status == B2BNegotiationStatus.Accepted
            ? await db.B2BDeals.AsNoTracking()
                .Where(d => d.B2BNegotiationId == negotiation.Id)
                .Select(d => (Guid?)d.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new B2BNegotiationDetailView(
            negotiation.Id,
            RoleOf(negotiation, merchantId.Value),
            negotiation.Status,
            listing?.Title ?? "Listing",
            listing?.Slug ?? string.Empty,
            merchantNames.GetValueOrDefault(negotiation.SellingMerchantProfileId, "Merchant"),
            merchantNames.GetValueOrDefault(negotiation.BuyingMerchantProfileId, "Merchant"),
            merchantNames.GetValueOrDefault(negotiation.CounterpartyOf(merchantId.Value), "Merchant"),
            listing?.WholesaleMinQuantity ?? 0,
            listing?.AllowMixedVariantB2B ?? false,
            negotiation.CurrentOfferHasExpired(clock.UtcNow),
            negotiation.AwaitingResponseFrom == merchantId,
            revisions,
            listing is null ? [] : BuildVariantOptions(listing),
            dealId);
    }

    public async Task<int> ExpireLapsedNegotiationsAsync(CancellationToken cancellationToken = default)
    {
        var openIds = await db.B2BNegotiations
            .AsNoTracking()
            .Where(n => n.Status == B2BNegotiationStatus.Open)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var id in openIds)
        {
            DetachTrackedGraph();

            var negotiation = await db.B2BNegotiations
                .Include(n => n.Revisions)
                .SingleOrDefaultAsync(n => n.Id == id && n.Status == B2BNegotiationStatus.Open, cancellationToken);

            if (negotiation is null || !negotiation.ExpireIfLapsed(clock.UtcNow))
            {
                continue;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                expired++;
                logger.LogInformation("B2B negotiation {NegotiationId} expired (offer lapsed)", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A participant accepted/rejected/countered it in the same moment; nothing to do.
                logger.LogInformation("B2B negotiation {NegotiationId} expiry was superseded", id);
            }
        }

        return expired;
    }

    // ---- Internals ---------------------------------------------------------

    private static B2BNegotiationParty RoleOf(B2BNegotiation n, Guid merchantId) =>
        merchantId == n.SellingMerchantProfileId ? B2BNegotiationParty.SellingMerchant : B2BNegotiationParty.BuyingMerchant;

    /// <summary>
    /// Common preconditions for a merchant making an offer on a listing. Returns a failed
    /// <see cref="Result"/> to translate, or <c>null</c> when the offer may proceed.
    /// </summary>
    private async Task<Result?> GuardOfferListingAsync(
        Listing listing, Guid buyerMerchantId, CancellationToken cancellationToken)
    {
        if (listing.MerchantProfileId == buyerMerchantId)
        {
            // Selling and buying merchants cannot be the same.
            return Result.Validation("You cannot make a wholesale offer on your own listing.");
        }

        if (listing.Status != ListingStatus.Live)
        {
            return Result.Conflict("This listing is not currently open for wholesale offers.");
        }

        if (!listing.AllowB2B)
        {
            return Result.Validation("This listing is not sold wholesale.");
        }

        var sellerApproved = await db.MerchantProfiles
            .AsNoTracking()
            .AnyAsync(
                m => m.Id == listing.MerchantProfileId && m.VerificationStatus == MerchantVerificationStatus.Approved,
                cancellationToken);
        if (!sellerApproved)
        {
            return Result.Conflict("This merchant is not currently accepting wholesale offers.");
        }

        return null;
    }

    private ProposedOffer? BuildProposedOffer(
        IReadOnlyList<B2BOfferLineInput>? lines,
        decimal unitPrice,
        string? message,
        int? validityDays,
        Listing listing,
        out string? error)
    {
        error = null;
        var requested = (lines ?? [])
            .Where(l => l.Quantity != 0)
            .ToList();

        if (requested.Count == 0)
        {
            error = "Choose at least one variant and quantity for the offer.";
            return null;
        }

        if (requested.Count > _options.MaxOfferLines)
        {
            error = $"An offer can include at most {_options.MaxOfferLines} variants.";
            return null;
        }

        if (requested.Select(l => l.VariantId).Distinct().Count() != requested.Count)
        {
            error = "An offer cannot list the same variant twice.";
            return null;
        }

        if (requested.Any(l => l.Quantity < 0 || l.Quantity > _options.MaxOfferLineQuantity))
        {
            error = $"Each variant quantity must be between 1 and {_options.MaxOfferLineQuantity}.";
            return null;
        }

        var sellableVariantIds = listing.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Id)
            .ToHashSet();
        if (requested.Any(l => !sellableVariantIds.Contains(l.VariantId)))
        {
            error = "One of the selected variants is not part of this listing.";
            return null;
        }

        if (unitPrice <= 0)
        {
            error = "Enter a proposed unit price greater than zero.";
            return null;
        }

        if (!B2BOfferRevision.HasJodPrecision(unitPrice))
        {
            error = "Enter the proposed unit price with no more than three decimal places for JOD.";
            return null;
        }

        var validity = validityDays is { } days
            ? TimeSpan.FromDays(Math.Clamp(days, 1, (int)_options.MaxOfferValidity.TotalDays))
            : _options.DefaultOfferValidity;
        if (validity < _options.MinOfferValidity)
        {
            validity = _options.MinOfferValidity;
        }
        else if (validity > _options.MaxOfferValidity)
        {
            validity = _options.MaxOfferValidity;
        }

        return new ProposedOffer(
            unitPrice,
            requested.Select(l => new ProposedOfferLine(l.VariantId, l.Quantity)).ToList(),
            message,
            clock.UtcNow.Add(validity));
    }

    private static OfferListingView BuildOfferListingView(Listing listing, string sellerName) => new(
        listing.Id,
        listing.Title,
        listing.Slug,
        sellerName,
        listing.WholesaleMinQuantity ?? 0,
        listing.AllowMixedVariantB2B,
        listing.WholesaleIndicativeUnitPrice,
        BuildVariantOptions(listing));

    private static IReadOnlyList<OfferVariantOption> BuildVariantOptions(Listing listing)
    {
        var optionNames = OptionNameByValueId(listing);
        return listing.Variants
            .Where(v => v.IsActive)
            .OrderBy(v => DescribeVariant(v, optionNames), StringComparer.OrdinalIgnoreCase)
            .Select(v => new OfferVariantOption(v.Id, DescribeVariant(v, optionNames), v.AvailableQuantity))
            .ToList();
    }

    private Task<Listing?> LoadListingAggregateBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Listings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .SingleOrDefaultAsync(l => l.Slug == slug, cancellationToken);

    private Task<Listing?> LoadListingAggregateByIdAsync(Guid listingId, CancellationToken cancellationToken) =>
        db.Listings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);

    private async Task<Dictionary<Guid, (string? Title, string? Slug)>> LoadListingHeadersAsync(
        IEnumerable<Guid> listingIds, CancellationToken cancellationToken)
    {
        var ids = listingIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Listings
            .AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.Title, l.Slug })
            .ToDictionaryAsync(x => x.Id, x => ((string?)x.Title, (string?)x.Slug), cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadMerchantNamesAsync(
        IEnumerable<Guid> merchantIds, CancellationToken cancellationToken)
    {
        var ids = merchantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.BusinessName })
            .ToDictionaryAsync(x => x.Id, x => x.BusinessName, cancellationToken);
    }

    private async Task<Result<Guid>> RequireEligibleMerchantAsync(
        string userId, CancellationToken cancellationToken)
    {
        if (await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Forbidden("Administrators cannot perform wholesale negotiation actions.");
        }

        var merchantId = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return merchantId is { } id
            ? Result<Guid>.Success(id)
            : Result<Guid>.Forbidden("Complete merchant verification before making wholesale offers.");
    }

    private async Task<Guid?> ResolveEligibleMerchantIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken))
        {
            return null;
        }

        return await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static Dictionary<Guid, (string OptionName, string Value)> OptionNameByValueId(Listing listing) =>
        listing.Options
            .SelectMany(o => o.Values.Select(v => new { v.Id, OptionName = o.Name, v.Value }))
            .ToDictionary(x => x.Id, x => (x.OptionName, x.Value));

    private static string DescribeVariant(
        ListingVariant variant, IReadOnlyDictionary<Guid, (string OptionName, string Value)> optionNames)
    {
        var parts = ListingQueries.DescribeOptions(variant, optionNames);
        return parts.Count == 0
            ? variant.Sku
            : string.Join(" · ", parts.Select(p => $"{p.Option}: {p.Value}"));
    }

    /// <summary>
    /// Clears the change tracker between negotiations in the expiry sweep so one negotiation's
    /// tracked graph cannot bleed into the next save. The runtime type is always
    /// <see cref="Faed.Web.Data.ApplicationDbContext"/>, a <see cref="DbContext"/>.
    /// </summary>
    private void DetachTrackedGraph()
    {
        if (db is DbContext context)
        {
            context.ChangeTracker.Clear();
        }
    }
}
