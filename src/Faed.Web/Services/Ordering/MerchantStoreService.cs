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
            return new MerchantStoreSettingsView([]);
        }

        var locations = await db.MerchantLocations
            .AsNoTracking()
            .Where(l => l.MerchantProfileId == merchantId)
            .OrderByDescending(l => l.IsActive).ThenBy(l => l.Name)
            .Select(l => new MerchantLocationView(
                l.Id, l.Name, l.AddressLine, l.Area, l.City, l.PickupInstructions, l.PickupHoursText, l.IsActive))
            .ToListAsync(cancellationToken);

        return new MerchantStoreSettingsView(locations);
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
