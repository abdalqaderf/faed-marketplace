using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faed.Web.Services.Listings;

/// <inheritdoc />
public sealed class ListingMediaService(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    IUserRoleService userRoles,
    ILogger<ListingMediaService> logger) : IListingMediaService
{
    public async Task<Result<StoredFileContent>> OpenImageAsync(
        string? userId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var media = await db.ListingMedia
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => new
            {
                m.StorageObjectKey,
                m.ContentType,
                m.OriginalFileName,
                m.ListingId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (media is null)
        {
            return Result<StoredFileContent>.NotFound("The image was not found.");
        }

        var listing = await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == media.ListingId)
            .Select(l => new
            {
                l.Status,
                l.MerchantProfileId,
                MerchantIsApproved = db.MerchantProfiles.Any(m =>
                    m.Id == l.MerchantProfileId && m.VerificationStatus == MerchantVerificationStatus.Approved),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return Result<StoredFileContent>.NotFound("The image was not found.");
        }

        // Public visibility rule: only a Live listing whose merchant is still Approved is
        // public (docs/03-BUSINESS-RULES.md §2, docs/11-ACCEPTANCE-CRITERIA.md "Public sees
        // only Live listings"). A merchant suspended after publishing must stop being
        // reachable by anonymous traffic even though their listings keep the Live status
        // (docs/17-DATA-INVARIANTS.md "A Live Listing's merchant must be approved"). SoldOut
        // is "addressable to authorized users" (docs/03 §2), not to anonymous public traffic —
        // a sold-out listing's photography is reachable only by the owning merchant or an
        // admin, same as Draft/PendingReview/Rejected/Hidden/Archived.
        var isPublic = listing.Status == ListingStatus.Live && listing.MerchantIsApproved;
        if (!isPublic)
        {
            var allowed = await IsOwnerOrAdminAsync(userId, listing.MerchantProfileId, cancellationToken);
            if (!allowed)
            {
                return Result<StoredFileContent>.Forbidden();
            }
        }

        var stream = await fileStorage.OpenReadAsync(media.StorageObjectKey, cancellationToken);
        if (stream is null)
        {
            logger.LogError(
                "Listing image {MediaId} has no backing file at {ObjectKey}", mediaId, media.StorageObjectKey);
            return Result<StoredFileContent>.NotFound("The stored file is no longer available.");
        }

        return Result<StoredFileContent>.Success(
            new StoredFileContent(stream, media.ContentType, media.OriginalFileName));
    }

    public async Task<Result<StoredFileContent>> OpenReferencePriceEvidenceAsync(
        string? userId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var evidence = await db.ListingReferencePriceEvidence
            .AsNoTracking()
            .Where(e => e.Id == evidenceId)
            .Select(e => new
            {
                e.StorageObjectKey,
                e.ContentType,
                e.OriginalFileName,
                e.ListingId,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (evidence?.StorageObjectKey is null)
        {
            return Result<StoredFileContent>.NotFound("The evidence file was not found.");
        }

        var listing = await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == evidence.ListingId)
            .Select(l => new { l.MerchantProfileId })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return Result<StoredFileContent>.NotFound("The evidence file was not found.");
        }

        // Reference-price evidence is never public — only the reviewing admin and the owning
        // merchant have any reason to see a supplier invoice or catalogue scan (AGENTS.md §8
        // "the reviewing admin sees them all", docs/03-BUSINESS-RULES.md §4).
        if (!await IsOwnerOrAdminAsync(userId, listing.MerchantProfileId, cancellationToken))
        {
            return Result<StoredFileContent>.Forbidden();
        }

        var stream = await fileStorage.OpenReadAsync(evidence.StorageObjectKey, cancellationToken);
        if (stream is null)
        {
            logger.LogError(
                "Reference-price evidence {EvidenceId} has no backing file at {ObjectKey}",
                evidenceId, evidence.StorageObjectKey);
            return Result<StoredFileContent>.NotFound("The stored file is no longer available.");
        }

        return Result<StoredFileContent>.Success(
            new StoredFileContent(stream, evidence.ContentType ?? "application/octet-stream", evidence.OriginalFileName ?? "evidence"));
    }

    private async Task<bool> IsOwnerOrAdminAsync(string? userId, Guid merchantProfileId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var isOwner = await db.MerchantProfiles
            .AsNoTracking()
            .AnyAsync(p => p.Id == merchantProfileId && p.UserId == userId, cancellationToken);

        return isOwner || await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken);
    }
}
