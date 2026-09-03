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
public sealed class B2BDealService(
    IApplicationDbContext db,
    IClock clock,
    IUserRoleService userRoles,
    IOptions<B2BDealOptions> options,
    ILogger<B2BDealService> logger) : IB2BDealService
{
    private const string StockConflictMessage =
        "Some of the offered stock is no longer available in the requested quantity. Reload the negotiation and try again.";

    private readonly B2BDealOptions _options = options.Value;

    // ---- Accept -> reserve -> deal ---------------------------------------------

    public async Task<Result<Guid>> AcceptOfferAsync(
        string merchantUserId, Guid negotiationId, AcceptOfferInput input, CancellationToken cancellationToken = default)
    {
        var merchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (merchant.Failed)
        {
            return Result<Guid>.From(merchant);
        }

        if (!Enum.IsDefined(input.FulfillmentType))
        {
            return Result<Guid>.Validation("Choose how the deal will be fulfilled.");
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var negotiation = await db.B2BNegotiations
            .Include(n => n.Revisions).ThenInclude(r => r.Lines)
            .SingleOrDefaultAsync(n => n.Id == negotiationId, cancellationToken);

        if (negotiation is null || !negotiation.IsParticipant(merchant.Value))
        {
            return Result<Guid>.NotFound("That negotiation was not found.");
        }

        // Both merchants must still be eligible to trade wholesale when the deal is created:
        // creating and reserving a B2BDeal is a fresh trade commitment, so a seller or buyer
        // suspended (or made an administrator) since the negotiation opened cannot be a party
        // to it (docs/03-BUSINESS-RULES.md §1, docs/16-PERMISSIONS-MATRIX.md; mirrors
        // OrderService re-checking the selling merchant at PlaceOrderAsync).
        var ineligible = await CounterpartyIneligibleAsync(negotiation, merchant.Value, cancellationToken);
        if (ineligible is not null)
        {
            return ineligible;
        }

        // A lapsed active offer is expired and persisted before it can be accepted
        // (docs/17-DATA-INVARIANTS.md "Only the active non-expired revision can be accepted").
        if (negotiation.ExpireIfLapsed(clock.UtcNow))
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A participant closed it in the same moment; the outcome is the same.
            }

            return Result<Guid>.Conflict("This offer has expired and can no longer be accepted.");
        }

        var listing = await db.Listings
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .SingleOrDefaultAsync(l => l.Id == negotiation.ListingId, cancellationToken);
        if (listing is null)
        {
            return Result<Guid>.Conflict("The listing behind this negotiation is no longer available.");
        }

        var revision = negotiation.CurrentRevision;
        var variantsById = listing.Variants.ToDictionary(v => v.Id);
        var optionNames = OptionNameByValueId(listing);

        B2BDeal deal;
        try
        {
            // Move the negotiation to Accepted first; if any line then fails to reserve, the
            // whole transaction rolls back and the negotiation stays Open in the database
            // (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §6 "If one variant fails, no variant is
            // reserved and no deal is created").
            negotiation.Accept(merchant.Value, clock.UtcNow);

            // The subtotal is the accepted revision's server-derived total and nothing else.
            // No shipping charge is agreed during negotiation, so none is added here — the
            // deal total equals the agreed subtotal (docs/03-BUSINESS-RULES.md §12,
            // docs/17-DATA-INVARIANTS.md). A seller-owned shipping cost, if one is ever
            // modelled, is recorded later by the seller, not injected at acceptance.
            deal = new B2BDeal(
                negotiation.Id,
                revision.Id,
                negotiation.SellingMerchantProfileId,
                negotiation.BuyingMerchantProfileId,
                input.FulfillmentType,
                shipmentReference: null,
                revision.ProposedUnitPrice,
                shippingCostSnapshot: null,
                revision.ProposedTotal,
                clock.UtcNow.Add(_options.ReservationWindow),
                clock.UtcNow);

            foreach (var line in revision.Lines)
            {
                if (!variantsById.TryGetValue(line.ListingVariantId, out var variant))
                {
                    return Result<Guid>.Conflict("A variant on this offer is no longer part of the listing.");
                }

                deal.AddLine(line.ListingVariantId, line.Quantity, revision.ProposedUnitPrice, DescribeVariant(variant, optionNames));

                // Atomic Available -> Reserved, protected by the variant's RowVersion. A stale
                // token here — or insufficient stock — rolls the whole acceptance back
                // (AGENTS.md §7, docs/17-DATA-INVARIANTS.md "No transaction may reserve more
                // than current available stock").
                variant.Reserve(line.Quantity, clock.UtcNow);
            }
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Conflict(ex.Message);
        }

        listing.RefreshAvailability(clock.UtcNow);
        // Force the listing row into the write set so a B2B acceptance and a competing B2C
        // order (or another acceptance) depleting different variants of the same listing
        // serialize on the listing row (docs/17-DATA-INVARIANTS.md).
        listing.RegisterStockReservation(clock.UtcNow);

        db.B2BDeals.Add(deal);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("B2B acceptance of negotiation {NegotiationId} lost a stock race", negotiationId);
            return Result<Guid>.Conflict(StockConflictMessage);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "B2B acceptance of negotiation {NegotiationId} failed to save", negotiationId);
            return Result<Guid>.Conflict(StockConflictMessage);
        }

        logger.LogInformation(
            "Merchant {MerchantId} accepted negotiation {NegotiationId}; created B2B deal {DealId} ({Units} units, total {Total})",
            merchant.Value, negotiationId, deal.Id, deal.TotalUnits, deal.TotalSnapshot);
        return Result<Guid>.Success(deal.Id);
    }

    // ---- Reads ---------------------------------------------------------------

    public async Task<IReadOnlyList<B2BDealSummaryView>> GetMyDealsAsync(
        string merchantUserId, B2BDealFilter filter, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return [];
        }

        var deals = (await db.B2BDeals
                .AsNoTracking()
                .Include(d => d.Lines)
                .Where(d => d.SellingMerchantProfileId == merchantId || d.BuyingMerchantProfileId == merchantId)
                .OrderByDescending(d => d.UpdatedAtUtc)
                .ToListAsync(cancellationToken))
            .Where(d => MatchesFilter(d.Status, filter))
            .ToList();

        var listingInfo = await LoadListingHeadersAsync(deals.Select(d => d.B2BNegotiationId), cancellationToken);
        var merchantNames = await LoadMerchantNamesAsync(
            deals.SelectMany(d => new[] { d.SellingMerchantProfileId, d.BuyingMerchantProfileId }), cancellationToken);

        return deals
            .Select(d => new B2BDealSummaryView(
                d.Id,
                RoleOf(d, merchantId.Value),
                d.Status,
                d.FulfillmentType,
                listingInfo.GetValueOrDefault(d.B2BNegotiationId).Title ?? "Listing",
                merchantNames.GetValueOrDefault(CounterpartyOf(d, merchantId.Value), "Merchant"),
                d.TotalUnits,
                d.TotalSnapshot,
                d.ReservationExpiresAtUtc,
                d.UpdatedAtUtc))
            .ToList();
    }

    public async Task<int> GetActionableDealCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return 0;
        }

        var active = await db.B2BDeals
            .AsNoTracking()
            .Where(d => (d.SellingMerchantProfileId == merchantId || d.BuyingMerchantProfileId == merchantId)
                && d.Status != B2BDealStatus.Completed && d.Status != B2BDealStatus.Cancelled)
            .Select(d => new { d.SellingMerchantProfileId, d.Status })
            .ToListAsync(cancellationToken);

        return active.Count(d => d.Status switch
        {
            B2BDealStatus.AwaitingFulfillment => d.SellingMerchantProfileId == merchantId,
            B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped => true,
            B2BDealStatus.Delivered => true,
            _ => false,
        });
    }

    public async Task<B2BDealDetailView?> GetDealAsync(
        string merchantUserId, Guid dealId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveEligibleMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return null;
        }

        var deal = await db.B2BDeals
            .AsNoTracking()
            .Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken);

        if (deal is null || !deal.IsParticipant(merchantId.Value))
        {
            // A merchant that is not a participant learns nothing — same as "not found".
            return null;
        }

        var negotiation = await db.B2BNegotiations
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == deal.B2BNegotiationId, cancellationToken);
        var listing = negotiation is null
            ? null
            : await db.Listings
                .AsNoTracking()
                .Select(l => new { l.Id, l.Title, l.Slug })
                .SingleOrDefaultAsync(l => l.Id == negotiation.ListingId, cancellationToken);

        var merchantNames = await LoadMerchantNamesAsync(
            new[] { deal.SellingMerchantProfileId, deal.BuyingMerchantProfileId }, cancellationToken);

        var lines = deal.Lines
            .OrderBy(l => l.VariantSnapshot, StringComparer.OrdinalIgnoreCase)
            .Select(l => new B2BDealLineView(l.VariantSnapshot, l.Quantity, l.UnitPriceSnapshot, l.LineTotalSnapshot))
            .ToList();

        return new B2BDealDetailView(
            deal.Id,
            deal.B2BNegotiationId,
            RoleOf(deal, merchantId.Value),
            deal.Status,
            deal.StatusReason,
            deal.FulfillmentType,
            deal.ShipmentReference,
            listing?.Title ?? "Listing",
            listing?.Slug ?? string.Empty,
            merchantNames.GetValueOrDefault(deal.SellingMerchantProfileId, "Merchant"),
            merchantNames.GetValueOrDefault(deal.BuyingMerchantProfileId, "Merchant"),
            merchantNames.GetValueOrDefault(CounterpartyOf(deal, merchantId.Value), "Merchant"),
            deal.AcceptedUnitPriceSnapshot,
            deal.ShippingCostSnapshot,
            deal.SubtotalSnapshot,
            deal.TotalSnapshot,
            deal.CreatedAtUtc,
            deal.ReservationExpiresAtUtc,
            deal.CompletedAtUtc,
            deal.CancelledAtUtc,
            lines);
    }

    // ---- Fulfilment transitions --------------------------------------------

    public Task<Result> MarkReadyForPickupAsync(string merchantUserId, Guid dealId, CancellationToken cancellationToken = default) =>
        SellerTransitionAsync(merchantUserId, dealId, d => d.MarkReadyForPickup(clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> MarkShippedAsync(
        string merchantUserId, Guid dealId, string? shipmentReference, CancellationToken cancellationToken = default) =>
        SellerTransitionAsync(
            merchantUserId, dealId, d => d.MarkShipped(shipmentReference, clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> SetShipmentReferenceAsync(
        string merchantUserId, Guid dealId, string shipmentReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shipmentReference))
        {
            return Task.FromResult(Result.Validation("Enter a shipment reference."));
        }

        return SellerTransitionAsync(
            merchantUserId, dealId, d => d.SetShipmentReference(shipmentReference, clock.UtcNow), StockEffect.None, cancellationToken);
    }

    public Task<Result> MarkDeliveredAsync(string merchantUserId, Guid dealId, CancellationToken cancellationToken = default) =>
        ParticipantTransitionAsync(merchantUserId, dealId, d => d.MarkDelivered(clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> CompleteAsync(string merchantUserId, Guid dealId, CancellationToken cancellationToken = default) =>
        ParticipantTransitionAsync(merchantUserId, dealId, d => d.Complete(clock.UtcNow), StockEffect.ConfirmSale, cancellationToken);

    public Task<Result> CancelAsync(
        string merchantUserId, Guid dealId, string reason, CancellationToken cancellationToken = default)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "Cancelled by a participating merchant." : reason.Trim();
        return ParticipantTransitionAsync(
            merchantUserId, dealId, d => d.Cancel(text, clock.UtcNow), StockEffect.Release, cancellationToken);
    }

    // ---- Expiry sweep -----------------------------------------------------------

    public async Task<int> ReleaseExpiredDealReservationsAsync(CancellationToken cancellationToken = default)
    {
        var dueIds = await db.B2BDeals
            .AsNoTracking()
            .Where(d => d.Status == B2BDealStatus.AwaitingFulfillment
                && d.ReservationExpiresAtUtc != null
                && d.ReservationExpiresAtUtc < clock.UtcNow)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var id in dueIds)
        {
            DetachTrackedGraph();

            var deal = await db.B2BDeals
                .Include(d => d.Lines)
                .SingleOrDefaultAsync(d => d.Id == id && d.Status == B2BDealStatus.AwaitingFulfillment, cancellationToken);

            // Fulfilled or cancelled by a participant since the id list was taken — idempotent
            // by construction (docs/17-DATA-INVARIANTS.md "Reservation release is idempotent").
            if (deal is null
                || deal.ReservationExpiresAtUtc is not { } expiresAt
                || expiresAt >= clock.UtcNow)
            {
                continue;
            }

            var outcome = await ApplyTransitionAsync(
                deal,
                d => d.Cancel("The reservation expired before the deal was fulfilled.", clock.UtcNow),
                StockEffect.Release,
                cancellationToken);

            if (outcome.Succeeded)
            {
                released++;
                logger.LogInformation("Released expired reservation for B2B deal {DealId}", id);
            }
            else if (outcome.ErrorKind == ResultErrorKind.Conflict)
            {
                logger.LogInformation("Expired-reservation release for B2B deal {DealId} was superseded", id);
            }
            else
            {
                logger.LogWarning("Expired-reservation release for B2B deal {DealId} failed: {Error}", id, outcome.Error);
            }
        }

        return released;
    }

    // ---- Internals ------------------------------------------------------------

    private enum StockEffect
    {
        None = 0,
        Release = 1,
        ConfirmSale = 2,
    }

    private async Task<Result> SellerTransitionAsync(
        string merchantUserId, Guid dealId, Action<B2BDeal> transition, StockEffect effect, CancellationToken cancellationToken)
    {
        var merchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (merchant.Failed)
        {
            return Result.Forbidden(merchant.Error!);
        }

        var deal = await db.B2BDeals
            .Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken);

        if (deal is null || !deal.IsParticipant(merchant.Value))
        {
            return Result.NotFound("That deal was not found.");
        }

        if (deal.SellingMerchantProfileId != merchant.Value)
        {
            return Result.Forbidden("Only the selling merchant can update fulfilment for this deal.");
        }

        return await ApplyTransitionAsync(deal, transition, effect, cancellationToken);
    }

    private async Task<Result> ParticipantTransitionAsync(
        string merchantUserId, Guid dealId, Action<B2BDeal> transition, StockEffect effect, CancellationToken cancellationToken)
    {
        var merchant = await RequireEligibleMerchantAsync(merchantUserId, cancellationToken);
        if (merchant.Failed)
        {
            return Result.Forbidden(merchant.Error!);
        }

        var deal = await db.B2BDeals
            .Include(d => d.Lines)
            .SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken);

        if (deal is null || !deal.IsParticipant(merchant.Value))
        {
            return Result.NotFound("That deal was not found.");
        }

        return await ApplyTransitionAsync(deal, transition, effect, cancellationToken);
    }

    /// <summary>
    /// Runs one deal status transition and its matching stock movement inside a single
    /// transaction: the two commit together or not at all (docs/06-ARCHITECTURE.md §6).
    /// </summary>
    private async Task<Result> ApplyTransitionAsync(
        B2BDeal deal, Action<B2BDeal> transition, StockEffect effect, CancellationToken cancellationToken)
    {
        try
        {
            transition(deal);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        List<Listing> affectedListings = [];
        if (effect != StockEffect.None)
        {
            var variantQuantities = deal.Lines
                .GroupBy(l => l.ListingVariantId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            var variantIds = variantQuantities.Keys.ToList();

            affectedListings = await db.Listings
                .Include(l => l.Variants)
                .Where(l => l.Variants.Any(v => variantIds.Contains(v.Id)))
                .ToListAsync(cancellationToken);

            var variantIndex = affectedListings
                .SelectMany(l => l.Variants)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionary(v => v.Id);

            try
            {
                foreach (var (variantId, quantity) in variantQuantities)
                {
                    if (!variantIndex.TryGetValue(variantId, out var variant))
                    {
                        return Result.Conflict("A variant on this deal could not be found.");
                    }

                    if (effect == StockEffect.Release)
                    {
                        variant.ReleaseReservation(quantity, clock.UtcNow);
                    }
                    else
                    {
                        variant.ConfirmSale(quantity, clock.UtcNow);
                    }
                }
            }
            catch (DomainException ex)
            {
                return Result.Conflict(ex.Message);
            }

            foreach (var listing in affectedListings)
            {
                listing.RefreshAvailability(clock.UtcNow);

                if (effect == StockEffect.Release)
                {
                    // A stock release must serialize on the listing row with a competing B2C
                    // reservation (or another release) touching a *different* variant of the
                    // same listing: otherwise both commit against a stale availability view and
                    // the listing can be left wrongly SoldOut or wrongly Live
                    // (docs/17-DATA-INVARIANTS.md; mirrors OrderService.PlaceOrderAsync). The
                    // loser gets a concurrency conflict and re-reads.
                    listing.RegisterStockRelease(clock.UtcNow);
                }
            }
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("This deal changed a moment ago. Reload it and try again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "B2B deal transition on {DealId} failed to save", deal.Id);
            return Result.Conflict("The change could not be saved. Reload the deal and try again.");
        }

        logger.LogInformation("B2B deal {DealId} moved to {Status}", deal.Id, deal.Status);
        return Result.Success();
    }

    private static bool MatchesFilter(B2BDealStatus status, B2BDealFilter filter) => filter switch
    {
        B2BDealFilter.Active => status != B2BDealStatus.Completed && status != B2BDealStatus.Cancelled,
        B2BDealFilter.AwaitingFulfillment => status == B2BDealStatus.AwaitingFulfillment,
        B2BDealFilter.InFulfillment => status is B2BDealStatus.ReadyForPickup or B2BDealStatus.Shipped or B2BDealStatus.Delivered,
        B2BDealFilter.Completed => status == B2BDealStatus.Completed,
        B2BDealFilter.Cancelled => status == B2BDealStatus.Cancelled,
        _ => true,
    };

    private static B2BNegotiationParty RoleOf(B2BDeal d, Guid merchantId) =>
        merchantId == d.SellingMerchantProfileId ? B2BNegotiationParty.SellingMerchant : B2BNegotiationParty.BuyingMerchant;

    private static Guid CounterpartyOf(B2BDeal d, Guid merchantId) =>
        merchantId == d.SellingMerchantProfileId ? d.BuyingMerchantProfileId : d.SellingMerchantProfileId;

    private async Task<Result<Guid>> RequireEligibleMerchantAsync(string userId, CancellationToken cancellationToken)
    {
        if (await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Forbidden("Administrators cannot perform wholesale deal actions.");
        }

        var merchantId = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return merchantId is { } id
            ? Result<Guid>.Success(id)
            : Result<Guid>.Forbidden("Complete merchant verification before acting on wholesale deals.");
    }

    /// <summary>
    /// Confirms both merchants on the negotiation are still eligible to trade wholesale before
    /// a deal is created: both must be <see cref="MerchantVerificationStatus.Approved"/> and
    /// neither may have become an administrator (docs/03-BUSINESS-RULES.md §1,
    /// docs/16-PERMISSIONS-MATRIX.md). The caller's own eligibility was already checked by
    /// <see cref="RequireEligibleMerchantAsync"/>; this re-checks the counterparty and, cheaply,
    /// the caller too. Returns a failed <see cref="Result{T}"/> to surface, or <c>null</c> when
    /// both may proceed.
    /// </summary>
    private async Task<Result<Guid>?> CounterpartyIneligibleAsync(
        B2BNegotiation negotiation, Guid callerMerchantId, CancellationToken cancellationToken)
    {
        var merchants = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == negotiation.SellingMerchantProfileId || m.Id == negotiation.BuyingMerchantProfileId)
            .Select(m => new { m.Id, m.UserId, m.VerificationStatus })
            .ToListAsync(cancellationToken);

        if (merchants.Count != 2 || merchants.Any(m => m.VerificationStatus != MerchantVerificationStatus.Approved))
        {
            return Result<Guid>.Conflict(
                "This deal can't be created: one of the two merchants is no longer an approved wholesale trader.");
        }

        var counterparty = merchants.Single(m => m.Id != callerMerchantId);
        if (await userRoles.IsInRoleAsync(counterparty.UserId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Conflict(
                "This deal can't be created: the other merchant account can no longer take part in wholesale negotiations.");
        }

        return null;
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

    private async Task<Dictionary<Guid, (string? Title, string? Slug)>> LoadListingHeadersAsync(
        IEnumerable<Guid> negotiationIds, CancellationToken cancellationToken)
    {
        var ids = negotiationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.B2BNegotiations
            .AsNoTracking()
            .Where(n => ids.Contains(n.Id))
            .Join(db.Listings.AsNoTracking(), n => n.ListingId, l => l.Id, (n, l) => new { n.Id, l.Title, l.Slug })
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
    /// Clears the change tracker between deals in the expiry sweep so one deal's tracked graph
    /// cannot bleed into the next deal's save. The runtime type is always
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
