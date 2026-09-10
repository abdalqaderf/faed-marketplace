using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Marketplace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Ordering;

/// <inheritdoc />
public sealed class OrderService(
    IApplicationDbContext db,
    IPublicMarketplaceService marketplace,
    IUserRoleService userRoles,
    IClock clock,
    IOptions<OrderingOptions> options,
    ILogger<OrderService> logger) : IOrderService
{
    private const string StockConflictMessage =
        "Some items are no longer available in the requested quantity. Review your order and try again.";

    private readonly OrderingOptions _options = options.Value;

    // ---- Reservation -------------------------------------------------------------

    public async Task<Result<ReservationView>> GetReservationAsync(
        string buyerUserId, string listingSlug, CancellationToken cancellationToken = default)
    {
        // Reuse the public read path: it already enforces Live + approved merchant + launch
        // sector, so a listing the buyer could never see is also one they can never reserve.
        if (await userRoles.IsInRoleAsync(buyerUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result<ReservationView>.Forbidden("Administrators cannot place B2C orders.");
        }

        if (!await CanBuyAsync(buyerUserId, cancellationToken))
        {
            return Result<ReservationView>.Forbidden("A Buyer or Merchant account is required to reserve an item.");
        }

        var listing = await marketplace.GetListingBySlugAsync(listingSlug, cancellationToken);
        if (listing is null)
        {
            return Result<ReservationView>.NotFound("That listing is not available to reserve.");
        }

        if (listing.RetailPrice is not { } unitPrice)
        {
            return Result<ReservationView>.Validation("This listing is not available to reserve.");
        }

        var lines = listing.Variants
            .Where(v => v.IsActive)
            .OrderBy(v => v.Combination, StringComparer.OrdinalIgnoreCase)
            .Select(v => new ReservationLineView(v.Id, v.Combination, unitPrice, v.AvailableQuantity))
            .ToList();

        // Only the pickup area is shown before the buyer reserves — the full address and the
        // shop's phone are revealed once the merchant confirms (docs/BUSINESS-MODEL.md §8.7).
        var pickups = await db.MerchantLocations
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == listing.MerchantProfileId && l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new ReservationPickupOption(l.Id, l.Name, l.Area))
            .ToListAsync(cancellationToken);

        return Result<ReservationView>.Success(new ReservationView(
            listing.Id,
            listing.Title,
            listing.Slug,
            listing.MerchantProfileId,
            listing.MerchantBusinessName,
            listing.MerchantSlug,
            $"Grade {listing.ConditionCode} — {listing.ConditionName}",
            lines,
            pickups));
    }

    // ---- Place order -------------------------------------------------------------

    public async Task<Result<Guid>> PlaceOrderAsync(
        string buyerUserId, PlaceOrderInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(buyerUserId))
        {
            return Result<Guid>.Forbidden("Sign in to place an order.");
        }

        // Defence in depth: the MVC route is behind the CanPlaceB2COrder policy, but the
        // service contract must not trust its caller.
        if (await userRoles.IsInRoleAsync(buyerUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Forbidden("Administrators cannot place B2C orders.");
        }

        if (!await CanBuyAsync(buyerUserId, cancellationToken))
        {
            return Result<Guid>.Forbidden("A Buyer or Merchant account is required to place an order.");
        }

        var requested = (input.Lines ?? [])
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        if (requested.Count == 0)
        {
            return Result<Guid>.Validation("Choose at least one item and quantity to order.");
        }

        if (requested.Values.Any(q => q > _options.MaxUnitsPerLine))
        {
            return Result<Guid>.Validation(
                $"You can order at most {_options.MaxUnitsPerLine} units of a single variant.");
        }

        if (!Enum.IsDefined(input.FulfillmentType))
        {
            return Result<Guid>.Validation("Choose how you want to receive the order.");
        }

        var contactName = (input.ContactName ?? string.Empty).Trim();
        var contactPhone = (input.ContactPhone ?? string.Empty).Trim();
        if (contactName.Length == 0 || contactPhone.Length == 0)
        {
            return Result<Guid>.Validation("Enter a contact name and phone number for the order.");
        }

        var variantIds = requested.Keys.ToList();

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var listings = await db.Listings
            .AsSplitQuery()
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .Include(l => l.DiscountReasons)
            .Where(l => l.Variants.Any(v => variantIds.Contains(v.Id)))
            .ToListAsync(cancellationToken);

        var variantIndex = listings
            .SelectMany(l => l.Variants, (l, v) => (Listing: l, Variant: v))
            .Where(x => variantIds.Contains(x.Variant.Id))
            .ToDictionary(x => x.Variant.Id);

        if (variantIndex.Count != variantIds.Count)
        {
            return Result<Guid>.NotFound("One of the selected items is no longer available.");
        }

        var orderListings = variantIndex.Values.Select(x => x.Listing).Distinct().ToList();

        // One order belongs to exactly one selling merchant. Faed does not run a multi-merchant cart.
        var merchantIds = orderListings.Select(l => l.MerchantProfileId).Distinct().ToList();
        if (merchantIds.Count != 1)
        {
            return Result<Guid>.Validation("All items in an order must be from the same merchant.");
        }

        var merchantProfileId = merchantIds[0];
        var merchantApproved = await db.MerchantProfiles
            .AsNoTracking()
            .AnyAsync(
                m => m.Id == merchantProfileId && m.VerificationStatus == MerchantVerificationStatus.Approved,
                cancellationToken);
        if (!merchantApproved)
        {
            return Result<Guid>.Conflict("This merchant is not currently accepting orders.");
        }

        foreach (var listing in orderListings)
        {
            if (listing.Status != ListingStatus.Live)
            {
                return Result<Guid>.Conflict("An item in your order is no longer available.");
            }

            if (listing.RetailPrice is null)
            {
                return Result<Guid>.Validation("An item in your order is not available for purchase.");
            }
        }

        // Fulfilment: pickup at a merchant location, or the merchant's own delivery to an
        // address. Faed does not model a delivery fee — the parties settle that directly. The
        // human-readable description in force now is snapshotted onto the order.
        string fulfillmentSnapshot;
        string? deliveryAddress = null;
        Guid? locationId = null;

        if (input.FulfillmentType == OrderFulfillmentType.Pickup)
        {
            var location = input.MerchantLocationId is { } lid
                ? await db.MerchantLocations.AsNoTracking().SingleOrDefaultAsync(
                    l => l.Id == lid && l.MerchantProfileId == merchantProfileId && l.IsActive, cancellationToken)
                : null;
            if (location is null)
            {
                return Result<Guid>.Validation("Choose a pickup location.");
            }

            locationId = location.Id;
            fulfillmentSnapshot = location.DescribeAddress()
                + (location.PickupHoursText is { } hours ? $" · Hours: {hours}" : string.Empty)
                + (location.PickupInstructions is { } note ? $" · {note}" : string.Empty);
        }
        else
        {
            deliveryAddress = (input.DeliveryAddressText ?? string.Empty).Trim();
            if (deliveryAddress.Length == 0)
            {
                return Result<Guid>.Validation("Enter the delivery address.");
            }

            fulfillmentSnapshot = $"Merchant delivery — {deliveryAddress}";
        }

        var gradeIds = orderListings.Select(l => l.ConditionGradeId).Distinct().ToList();
        var grades = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => gradeIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => $"Grade {g.Code} — {g.Name}", cancellationToken);

        var reasonIds = orderListings
            .SelectMany(l => l.DiscountReasons.Select(r => r.DiscountReasonId))
            .Distinct()
            .ToList();
        var reasonNames = reasonIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.DiscountReasons
                .AsNoTracking()
                .Where(r => reasonIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var now = clock.UtcNow;
        Order order;
        try
        {
            order = new Order(
                buyerUserId,
                merchantProfileId,
                input.FulfillmentType,
                locationId,
                fulfillmentSnapshot,
                deliveryAddress,
                contactName,
                contactPhone,
                input.BuyerNote,
                now.Add(_options.ReservationWindow),
                now);

            foreach (var (variantId, quantity) in requested)
            {
                var (listing, variant) = variantIndex[variantId];
                var optionNames = OptionNameByValueId(listing);
                var combination = DescribeVariant(variant, optionNames);
                var discountSnapshot = listing.DiscountReasons.Count == 0
                    ? null
                    : string.Join(", ", listing.DiscountReasons
                        .Select(r => reasonNames.GetValueOrDefault(r.DiscountReasonId))
                        .Where(name => name is not null));

                order.AddItem(
                    listing.Id,
                    variant.Id,
                    quantity,
                    listing.RetailPrice!.Value,
                    listing.Title,
                    combination,
                    grades.GetValueOrDefault(listing.ConditionGradeId, "Condition not recorded"),
                    string.IsNullOrWhiteSpace(discountSnapshot) ? null : discountSnapshot);

                // Atomic Available -> Reserved. A stale RowVersion here means another order
                // took the stock first; the whole transaction rolls back.
                variant.Reserve(quantity, now);
            }
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Conflict(ex.Message);
        }

        foreach (var listing in orderListings)
        {
            listing.RefreshAvailability(now);
            // Force this listing's rowversion into the transaction's write set so two orders
            // depleting different variants of the same listing serialize on the listing row —
            // otherwise both could commit against a listing each still sees as in stock,
            // leaving a fully depleted listing incorrectly Live.
            listing.RegisterStockReservation(now);
        }

        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Order placement for buyer {BuyerId} lost a stock race", buyerUserId);
            return Result<Guid>.Conflict(StockConflictMessage);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Order placement for buyer {BuyerId} failed to save", buyerUserId);
            return Result<Guid>.Conflict(StockConflictMessage);
        }

        logger.LogInformation(
            "Buyer {BuyerId} placed order {OrderId} with merchant {MerchantId} ({Units} units, total {Total})",
            buyerUserId, order.Id, merchantProfileId, order.TotalUnits, order.Total);

        return Result<Guid>.Success(order.Id);
    }

    // ---- Buyer reads / actions --------------------------------------------------

    public Task<PagedResult<OrderSummaryView>> GetMyOrdersAsync(
        string buyerUserId, int page = 1, CancellationToken cancellationToken = default)
    {
        return db.Orders
            .AsNoTracking()
            .Where(o => o.BuyerUserId == buyerUserId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderSummaryView(
                o.Id,
                o.Reference,
                o.Status,
                o.FulfillmentType,
                db.MerchantProfiles.Where(m => m.Id == o.MerchantProfileId)
                    .Select(m => m.BusinessName).FirstOrDefault() ?? "Merchant",
                o.Items.Sum(i => i.Quantity),
                o.Total,
                o.CreatedAtUtc,
                o.ReservationExpiresAtUtc))
            .ToPagedResultAsync(page, Paging.DefaultPageSize, cancellationToken);
    }

    public async Task<OrderDetailView?> GetMyOrderAsync(
        string buyerUserId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == buyerUserId, cancellationToken);

        return order is null ? null : await ToDetailViewAsync(order, cancellationToken);
    }

    public async Task<Result> CancelMyOrderAsync(
        string buyerUserId, Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == buyerUserId, cancellationToken);

        if (order is null)
        {
            return Result.NotFound("That order was not found.");
        }

        if (!order.BuyerCanCancel)
        {
            return Result.Conflict(
                "This order can no longer be cancelled here. Contact the merchant if you need to change it.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? "Cancelled by the buyer." : reason.Trim();
        return await ApplyTransitionAsync(
            order, o => o.Cancel(text, clock.UtcNow), StockEffect.Release, cancellationToken);
    }

    public async Task<Result> ConfirmReceiptAsync(
        string buyerUserId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == buyerUserId, cancellationToken);

        if (order is null)
        {
            return Result.NotFound("That order was not found.");
        }

        // The buyer confirms receipt once the merchant has handed the order over — the same
        // transition the merchant's own "mark completed" uses.
        return await ApplyTransitionAsync(
            order, o => o.Complete(clock.UtcNow), StockEffect.ConfirmSale, cancellationToken);
    }

    // ---- Merchant reads / actions ---------------------------------------------

    public async Task<PagedResult<OrderSummaryView>> GetMerchantOrdersAsync(
        string merchantUserId, MerchantOrderFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return PagedResult<OrderSummaryView>.Empty(page, Paging.DefaultPageSize);
        }

        var query = db.Orders.AsNoTracking().Where(o => o.MerchantProfileId == merchantId);

        query = filter switch
        {
            MerchantOrderFilter.Open => query.Where(o =>
                o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.NoShow),
            MerchantOrderFilter.NeedsConfirmation => query.Where(o => o.Status == OrderStatus.Pending),
            MerchantOrderFilter.InFulfillment => query.Where(o =>
                o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.ReadyForPickup || o.Status == OrderStatus.OutForDelivery),
            MerchantOrderFilter.Completed => query.Where(o => o.Status == OrderStatus.Completed),
            MerchantOrderFilter.Cancelled => query.Where(o =>
                o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.NoShow),
            _ => query,
        };

        return await query
            // Oldest waiting order first: the queue is a work list.
            .OrderBy(o => o.Status == OrderStatus.Pending ? 0 : 1)
            .ThenBy(o => o.CreatedAtUtc)
            .Select(o => new OrderSummaryView(
                o.Id,
                o.Reference,
                o.Status,
                o.FulfillmentType,
                o.ContactName,
                o.Items.Sum(i => i.Quantity),
                o.Total,
                o.CreatedAtUtc,
                o.ReservationExpiresAtUtc))
            .ToPagedResultAsync(page, Paging.DefaultPageSize, cancellationToken);
    }

    public async Task<int> GetMerchantOpenOrderCountAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return 0;
        }

        return await db.Orders
            .AsNoTracking()
            .CountAsync(o => o.MerchantProfileId == merchantId && o.Status == OrderStatus.Pending, cancellationToken);
    }

    public async Task<OrderDetailView?> GetMerchantOrderAsync(
        string merchantUserId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return null;
        }

        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.MerchantProfileId == merchantId, cancellationToken);

        return order is null ? null : await ToDetailViewAsync(order, cancellationToken);
    }

    public Task<Result> ConfirmAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default) =>
        MerchantTransitionAsync(
            merchantUserId, orderId, o => o.Confirm(clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> MarkReadyForPickupAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default) =>
        MerchantTransitionAsync(
            merchantUserId, orderId, o => o.MarkReadyForPickup(clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> MarkOutForDeliveryAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default) =>
        MerchantTransitionAsync(
            merchantUserId, orderId, o => o.MarkOutForDelivery(clock.UtcNow), StockEffect.None, cancellationToken);

    public Task<Result> CompleteAsync(string merchantUserId, Guid orderId, CancellationToken cancellationToken = default) =>
        MerchantTransitionAsync(
            merchantUserId, orderId, o => o.Complete(clock.UtcNow), StockEffect.ConfirmSale, cancellationToken);

    public Task<Result> MarkNoShowAsync(
        string merchantUserId, Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(Result.Validation("Enter a reason for the no-show."));
        }

        var text = reason.Trim();
        return MerchantTransitionAsync(
            merchantUserId, orderId, o => o.MarkNoShow(text, clock.UtcNow), StockEffect.Release, cancellationToken);
    }

    public Task<Result> CancelAsMerchantAsync(
        string merchantUserId, Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(Result.Validation("Enter a reason for the cancellation."));
        }

        var text = reason.Trim();
        return MerchantTransitionAsync(
            merchantUserId, orderId, o => o.Cancel(text, clock.UtcNow), StockEffect.Release, cancellationToken);
    }

    // ---- Deadline sweeps ------------------------------------------------------

    public Task<int> ReleaseExpiredReservationsAsync(CancellationToken cancellationToken = default) =>
        SweepPastDeadlineAsync(
            due: o => o.Status == OrderStatus.Pending
                && o.ReservationExpiresAtUtc != null
                && o.ReservationExpiresAtUtc < clock.UtcNow,
            stillDue: o => o.Status == OrderStatus.Pending
                && o.ReservationExpiresAtUtc is { } expiresAt && expiresAt < clock.UtcNow,
            transition: o => o.Cancel(
                "The shop did not respond in time, so the reservation was cancelled — you lost nothing.",
                clock.UtcNow),
            label: "expired reservation",
            cancellationToken);

    public Task<int> ExpireUnhandledConfirmedOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow - _options.NoShowWindow;
        return SweepPastDeadlineAsync(
            due: o => o.Status == OrderStatus.Confirmed
                && o.ConfirmedAtUtc != null && o.ConfirmedAtUtc < cutoff,
            stillDue: o => o.Status == OrderStatus.Confirmed
                && o.ConfirmedAtUtc is { } confirmedAt && confirmedAt < cutoff,
            transition: o => o.MarkNoShow(
                "The order was not prepared for pickup within 48 hours of being confirmed, so the reservation was released.",
                clock.UtcNow),
            label: "unhandled confirmed order",
            cancellationToken);
    }

    public Task<int> CloseStaleReadyOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow - _options.AutoCloseWindow;
        return SweepPastDeadlineAsync(
            // No dedicated "became ready" timestamp: an order with no movement since it was
            // marked ready has UpdatedAtUtc frozen at that moment, which is exactly the clock
            // this deadline runs against.
            due: o => (o.Status == OrderStatus.ReadyForPickup || o.Status == OrderStatus.OutForDelivery)
                && o.UpdatedAtUtc < cutoff,
            stillDue: o => (o.Status == OrderStatus.ReadyForPickup || o.Status == OrderStatus.OutForDelivery)
                && o.UpdatedAtUtc < cutoff,
            transition: o => o.MarkNoShow(
                "This order was ready for pickup for 72 hours with no collection, so it was closed and the stock released.",
                clock.UtcNow),
            label: "stale ready order",
            cancellationToken);
    }

    /// <summary>
    /// The shared shape of every deadline sweep: take the ids past the deadline now, then
    /// re-load and re-check each one before acting, so a merchant confirmation or cancellation
    /// landing in the same moment simply wins and the sweep does nothing. Each order moves in
    /// its own transaction (<see cref="ApplyTransitionAsync"/>), so one failure never blocks
    /// the rest of the batch.
    /// </summary>
    private async Task<int> SweepPastDeadlineAsync(
        System.Linq.Expressions.Expression<Func<Order, bool>> due,
        Func<Order, bool> stillDue,
        Action<Order> transition,
        string label,
        CancellationToken cancellationToken)
    {
        var dueIds = await db.Orders.AsNoTracking().Where(due).Select(o => o.Id).ToListAsync(cancellationToken);

        var resolved = 0;
        foreach (var id in dueIds)
        {
            DetachTrackedGraph();

            var order = await db.Orders
                .Include(o => o.Items)
                .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

            if (order is null || !stillDue(order))
            {
                continue;
            }

            var outcome = await ApplyTransitionAsync(order, transition, StockEffect.Release, cancellationToken);
            if (outcome.Succeeded)
            {
                resolved++;
                logger.LogInformation("Deadline sweep closed {Label} {OrderId}", label, id);
            }
            else if (outcome.ErrorKind == ResultErrorKind.Conflict)
            {
                // A merchant or buyer action landed in the same moment; nothing to do.
                logger.LogInformation("Deadline sweep for {Label} {OrderId} was superseded", label, id);
            }
            else
            {
                logger.LogWarning("Deadline sweep for {Label} {OrderId} failed: {Error}", label, id, outcome.Error);
            }
        }

        return resolved;
    }

    // ---- Internals ------------------------------------------------------------

    private enum StockEffect
    {
        None = 0,
        Release = 1,
        ConfirmSale = 2,
    }

    private async Task<Result> MerchantTransitionAsync(
        string merchantUserId,
        Guid orderId,
        Action<Order> transition,
        StockEffect effect,
        CancellationToken cancellationToken)
    {
        var merchantId = await ResolveMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return Result.Forbidden("Complete merchant verification to manage orders.");
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == orderId && o.MerchantProfileId == merchantId, cancellationToken);

        if (order is null)
        {
            return Result.NotFound("That order was not found.");
        }

        return await ApplyTransitionAsync(order, transition, effect, cancellationToken);
    }

    /// <summary>
    /// Runs one order status transition and its matching stock movement inside a single
    /// transaction: the two commit together or not at all.
    /// </summary>
    private async Task<Result> ApplyTransitionAsync(
        Order order,
        Action<Order> transition,
        StockEffect effect,
        CancellationToken cancellationToken)
    {
        try
        {
            transition(order);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        List<Listing> affectedListings = [];
        if (effect != StockEffect.None)
        {
            var variantQuantities = order.Items
                .GroupBy(i => i.ListingVariantId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
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
                        return Result.Conflict("A variant on this order could not be found.");
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
            return Result.Conflict("This order changed a moment ago. Reload it and try again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Order transition on {OrderId} failed to save", order.Id);
            return Result.Conflict("The change could not be saved. Reload the order and try again.");
        }

        logger.LogInformation("Order {OrderId} moved to {Status}", order.Id, order.Status);
        return Result.Success();
    }

    private Task<Guid?> ResolveMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> CanBuyAsync(string userId, CancellationToken cancellationToken) =>
        await userRoles.IsInRoleAsync(userId, FaedRoles.Buyer, cancellationToken)
        || await userRoles.IsInRoleAsync(userId, FaedRoles.Merchant, cancellationToken);

    private async Task<OrderDetailView> ToDetailViewAsync(Order order, CancellationToken cancellationToken)
    {
        var merchant = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == order.MerchantProfileId)
            .Select(m => new { m.BusinessName, m.PublicSlug, m.ContactPhone })
            .SingleOrDefaultAsync(cancellationToken);

        var pickupArea = order.MerchantLocationId is { } locationId
            ? await db.MerchantLocations.AsNoTracking()
                .Where(l => l.Id == locationId).Select(l => l.Area).SingleOrDefaultAsync(cancellationToken)
            : null;

        // The reveal rule: the shop's area is always visible; its full address (carried on the
        // fulfilment snapshot) and phone only after the merchant confirms.
        var revealed = order.ConfirmedAtUtc is not null;

        var listingSlugs = await db.Listings
            .AsNoTracking()
            .Where(l => order.Items.Select(i => i.ListingId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Slug, cancellationToken);

        var items = order.Items
            .OrderBy(i => i.ListingTitleSnapshot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.VariantSnapshot, StringComparer.OrdinalIgnoreCase)
            .Select(i => new OrderLineView(
                i.ListingTitleSnapshot,
                listingSlugs.GetValueOrDefault(i.ListingId),
                i.VariantSnapshot,
                i.ConditionGradeSnapshot,
                i.DiscountReasonSnapshot,
                i.Quantity,
                i.UnitPriceSnapshot,
                i.LineTotalSnapshot))
            .ToList();

        return new OrderDetailView(
            order.Id,
            order.Reference,
            order.Status,
            order.StatusReason,
            order.FulfillmentType,
            order.FulfillmentSnapshot,
            order.DeliveryAddressText,
            order.Subtotal,
            order.Total,
            order.ContactName,
            order.ContactPhone,
            order.BuyerNote,
            order.CreatedAtUtc,
            order.ConfirmedAtUtc,
            order.CompletedAtUtc,
            order.CancelledAtUtc,
            order.ReservationExpiresAtUtc,
            order.MerchantProfileId,
            merchant?.BusinessName ?? "Merchant",
            merchant?.PublicSlug ?? string.Empty,
            string.IsNullOrWhiteSpace(pickupArea) ? "Amman" : pickupArea,
            revealed ? merchant?.ContactPhone : null,
            items);
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
    /// Clears the change tracker between orders in the expiry sweep so one order's tracked
    /// graph cannot bleed into the next order's save. The runtime type is always
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
