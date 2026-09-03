using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Faed.Web.Services.Ordering;

/// <inheritdoc />
public sealed class MerchantStoreService(IApplicationDbContext db, IClock clock) : IMerchantStoreService
{
    public async Task<MerchantStoreSettingsView> GetSettingsAsync(
        string merchantUserId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return new MerchantStoreSettingsView([], []);
        }

        var locations = await db.MerchantLocations
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == merchantId)
            .OrderByDescending(l => l.IsActive).ThenBy(l => l.Name)
            .Select(l => new MerchantLocationView(
                l.Id, l.Name, l.AddressLine, l.Area, l.City, l.PickupInstructions, l.PickupHoursText, l.IsActive))
            .ToListAsync(cancellationToken);

        var zones = await db.MerchantDeliveryZones
            .AsNoTracking()
            .Where(z => z.MerchantProfileId == merchantId)
            .OrderByDescending(z => z.IsActive).ThenBy(z => z.Name)
            .Select(z => new MerchantDeliveryZoneView(
                z.Id, z.Name, z.DeliveryFee, z.MinimumOrderValue, z.EstimatedDeliveryText, z.IsActive))
            .ToListAsync(cancellationToken);

        return new MerchantStoreSettingsView(locations, zones);
    }

    public async Task<Result<Guid>> AddLocationAsync(
        string merchantUserId, MerchantLocationInput input, CancellationToken cancellationToken = default)
    {
        var merchantId = await RequireMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return Result<Guid>.Forbidden("Complete merchant verification before configuring your store.");
        }

        try
        {
            var location = new MerchantLocation(
                merchantId.Value, input.Name, input.AddressLine, input.Area, input.City,
                input.PickupInstructions, input.PickupHoursText, clock.UtcNow);
            db.MerchantLocations.Add(location);
            await db.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(location.Id);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Validation(ex.Message);
        }
    }

    public Task<Result> UpdateLocationAsync(
        string merchantUserId, Guid locationId, MerchantLocationInput input, CancellationToken cancellationToken = default) =>
        MutateLocationAsync(merchantUserId, locationId, (location, now) =>
            location.Update(input.Name, input.AddressLine, input.Area, input.City,
                input.PickupInstructions, input.PickupHoursText, now), cancellationToken);

    public Task<Result> SetLocationActiveAsync(
        string merchantUserId, Guid locationId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateLocationAsync(merchantUserId, locationId, (location, now) => location.SetActive(isActive, now), cancellationToken);

    public async Task<Result<Guid>> AddDeliveryZoneAsync(
        string merchantUserId, MerchantDeliveryZoneInput input, CancellationToken cancellationToken = default)
    {
        var merchantId = await RequireMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return Result<Guid>.Forbidden("Complete merchant verification before configuring your store.");
        }

        try
        {
            var zone = new MerchantDeliveryZone(
                merchantId.Value, input.Name, input.DeliveryFee, input.MinimumOrderValue,
                input.EstimatedDeliveryText, clock.UtcNow);
            db.MerchantDeliveryZones.Add(zone);
            await db.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(zone.Id);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Validation(ex.Message);
        }
    }

    public Task<Result> UpdateDeliveryZoneAsync(
        string merchantUserId, Guid zoneId, MerchantDeliveryZoneInput input, CancellationToken cancellationToken = default) =>
        MutateZoneAsync(merchantUserId, zoneId, (zone, now) =>
            zone.Update(input.Name, input.DeliveryFee, input.MinimumOrderValue, input.EstimatedDeliveryText, now),
            cancellationToken);

    public Task<Result> SetDeliveryZoneActiveAsync(
        string merchantUserId, Guid zoneId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateZoneAsync(merchantUserId, zoneId, (zone, now) => zone.SetActive(isActive, now), cancellationToken);

    private async Task<Result> MutateLocationAsync(
        string merchantUserId, Guid locationId, Action<MerchantLocation, DateTime> mutate, CancellationToken cancellationToken)
    {
        var merchantId = await RequireMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return Result.Forbidden("Complete merchant verification before configuring your store.");
        }

        var location = await db.MerchantLocations
            .SingleOrDefaultAsync(l => l.Id == locationId && l.MerchantProfileId == merchantId, cancellationToken);
        if (location is null)
        {
            return Result.NotFound("That location was not found.");
        }

        try
        {
            mutate(location, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Validation(ex.Message);
        }
    }

    private async Task<Result> MutateZoneAsync(
        string merchantUserId, Guid zoneId, Action<MerchantDeliveryZone, DateTime> mutate, CancellationToken cancellationToken)
    {
        var merchantId = await RequireMerchantIdAsync(merchantUserId, cancellationToken);
        if (merchantId is null)
        {
            return Result.Forbidden("Complete merchant verification before configuring your store.");
        }

        var zone = await db.MerchantDeliveryZones
            .SingleOrDefaultAsync(z => z.Id == zoneId && z.MerchantProfileId == merchantId, cancellationToken);
        if (zone is null)
        {
            return Result.NotFound("That delivery zone was not found.");
        }

        try
        {
            mutate(zone, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Validation(ex.Message);
        }
    }

    private Task<Guid?> ResolveMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Guid?> RequireMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
