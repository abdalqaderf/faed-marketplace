using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Listings;

/// <inheritdoc />
public sealed class InventoryService(
    IApplicationDbContext db,
    IClock clock,
    ILogger<InventoryService> logger) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryRow>> GetMyInventoryAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(userId, cancellationToken);
        if (merchantId is null)
        {
            return [];
        }

        var listings = await db.Listings
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == merchantId && l.Status != ListingStatus.Archived)
            .Include(l => l.Options).ThenInclude(o => o.Values)
            .Include(l => l.Variants).ThenInclude(v => v.OptionValues)
            .ToListAsync(cancellationToken);

        return
        [
            .. listings
                .SelectMany(listing =>
                {
                    var optionNameByValueId = listing.Options
                        .SelectMany(o => o.Values.Select(v => new { v.Id, OptionName = o.Name, v.Value }))
                        .ToDictionary(x => x.Id, x => (x.OptionName, x.Value));

                    return listing.Variants.Select(variant => new InventoryRow(
                        variant.Id,
                        listing.Id,
                        listing.Title,
                        listing.Status,
                        variant.Sku,
                        ListingQueries.DescribeOptions(variant, optionNameByValueId),
                        variant.AvailableQuantity,
                        variant.ReservedQuantity,
                        variant.SoldQuantity,
                        variant.IsActive,
                        variant.UpdatedAtUtc));
                })
                // Whatever is closest to running out is what the merchant needs to see first
                // (.claude/skills/faed-dashboard-ux "what needs my attention now?").
                .OrderBy(r => r.IsActive ? 0 : 1)
                .ThenBy(r => r.AvailableQuantity)
                .ThenBy(r => r.ListingTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Sku, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<IReadOnlyList<InventoryAdjustmentView>> GetMyRecentAdjustmentsAsync(
        string userId, int take = 25, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(userId, cancellationToken);
        if (merchantId is null)
        {
            return [];
        }

        return await (
            from adjustment in db.InventoryAdjustments.AsNoTracking()
            join variant in db.ListingVariants.AsNoTracking()
                on adjustment.ListingVariantId equals variant.Id
            join listing in db.Listings.AsNoTracking()
                on variant.ListingId equals listing.Id
            where listing.MerchantProfileId == merchantId
            orderby adjustment.CreatedAtUtc descending
            select new InventoryAdjustmentView(
                adjustment.Id,
                variant.Id,
                variant.Sku,
                listing.Title,
                adjustment.AdjustmentType,
                adjustment.QuantityDelta,
                adjustment.QuantityBefore,
                adjustment.QuantityAfter,
                adjustment.Reason,
                adjustment.CreatedAtUtc))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<int>> AdjustStockAsync(
        string userId, StockAdjustmentInput input, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(input.AdjustmentType))
        {
            return Result<int>.Validation("Choose a valid adjustment reason.");
        }

        if (input.QuantityDelta == 0)
        {
            return Result<int>.Validation("Enter how many units to add or remove.");
        }

        var reason = (input.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return Result<int>.Validation("Explain why the stock is changing.");
        }

        if (reason.Length > InventoryAdjustment.MaxReasonLength)
        {
            return Result<int>.Validation(
                $"The reason must be {InventoryAdjustment.MaxReasonLength} characters or fewer.");
        }

        // A "found stock" record that removes units, or a "lost" one that adds them, would make
        // the audit trail lie about what happened.
        if (input.AdjustmentType == InventoryAdjustmentType.StockFound && input.QuantityDelta < 0)
        {
            return Result<int>.Validation("Found stock adds units. Use a correction to remove them.");
        }

        if (input.AdjustmentType == InventoryAdjustmentType.StockLostOrDamaged && input.QuantityDelta > 0)
        {
            return Result<int>.Validation("Damaged or lost stock removes units. Use a correction to add them.");
        }

        var merchantId = await ResolveMerchantIdAsync(userId, cancellationToken);
        if (merchantId is null)
        {
            return Result<int>.Forbidden("Complete merchant verification before managing inventory.");
        }

        // The listing is loaded with the variant so publication can follow stock in the same
        // transaction: a published listing that runs out becomes SoldOut, and comes back when
        // it is restocked (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §2).
        var listing = await db.Listings
            .Include(l => l.Variants)
            .SingleOrDefaultAsync(
                l => l.MerchantProfileId == merchantId && l.Variants.Any(v => v.Id == input.VariantId),
                cancellationToken);

        var variant = listing?.Variants.SingleOrDefault(v => v.Id == input.VariantId);
        if (listing is null || variant is null)
        {
            return Result<int>.NotFound("That variant was not found.");
        }

        if (listing.Status == ListingStatus.Archived)
        {
            // An archived listing is no longer managed (Listing.RequireNotArchived mirrors
            // this for every other mutator); the inventory screen already hides these rows,
            // but a direct POST must not be able to move their stock either.
            return Result<int>.Validation("This listing is archived and its stock can no longer be adjusted.");
        }

        var before = variant.AvailableQuantity;
        int after;
        try
        {
            after = variant.AdjustAvailable(input.QuantityDelta, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result<int>.Validation(ex.Message);
        }

        db.InventoryAdjustments.Add(new InventoryAdjustment(
            variant.Id,
            userId,
            input.AdjustmentType,
            input.QuantityDelta,
            before,
            after,
            reason,
            clock.UtcNow));

        // Sum every other active variant fresh from the database rather than trusting the
        // navigation collection loaded at the start of this request: a concurrent request
        // depleting a *different* variant on the same listing would otherwise still look
        // in-stock here, and neither request would ever flip a jointly-depleted listing to
        // SoldOut (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §2).
        var siblingTotal = await db.ListingVariants
            .AsNoTracking()
            .Where(v => v.ListingId == listing.Id && v.IsActive && v.Id != variant.Id)
            .SumAsync(v => v.AvailableQuantity, cancellationToken);
        var currentAvailableUnits = siblingTotal + (variant.IsActive ? after : 0);
        listing.RefreshAvailability(currentAvailableUnits, clock.UtcNow);

        // The quantity, its audit row and any resulting publication change commit together or
        // not at all: stock must never move without the record of why (AGENTS.md §7).
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The variant rowversion moved under us. Nothing is persisted; the merchant retries
            // against current stock (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §9).
            logger.LogInformation("Stock adjustment on variant {VariantId} hit a concurrency conflict", variant.Id);
            return Result<int>.Conflict("This stock changed a moment ago. Reload the page and try again.");
        }
        catch (DbUpdateException ex)
        {
            // The domain guard already refuses a negative result before this point, so the
            // CK_ListingVariants_Quantities_NonNegative backstop should not fire from here in
            // practice — but a raw DB exception must never reach the caller
            // (docs/06-ARCHITECTURE.md §9), so this is a safety net rather than a silent 500.
            logger.LogError(ex, "Stock adjustment on variant {VariantId} failed to save", variant.Id);
            return Result<int>.Conflict("The adjustment could not be saved. Reload the page and try again.");
        }

        logger.LogInformation(
            "Variant {VariantId} stock adjusted by {Delta} ({Type}) from {Before} to {After}",
            variant.Id, input.QuantityDelta, input.AdjustmentType, before, after);

        return Result<int>.Success(after);
    }

    private Task<Guid?> ResolveMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
