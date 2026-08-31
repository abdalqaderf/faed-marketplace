using Faed.Application.Abstractions;
using Faed.Application.Common;
using Faed.Domain.Authorization;
using Faed.Domain.Entities;
using Faed.Domain.Enums;
using Faed.Domain.Exceptions;
using Faed.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Application.Merchants;

/// <inheritdoc />
public sealed class MerchantVerificationService(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    IUserRoleService userRoles,
    IClock clock,
    IOptions<MerchantVerificationOptions> options,
    ILogger<MerchantVerificationService> logger) : IMerchantVerificationService
{
    private const string VerificationContainer = "merchant-verification";
    private const string MerchantProfileTargetType = nameof(MerchantProfile);

    private readonly MerchantVerificationOptions _options = options.Value;

    public async Task<MerchantApplicationView?> GetMyApplicationAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Include(p => p.Documents)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile is null ? null : MapApplication(profile);
    }

    public async Task<Result<Guid>> SaveDraftAsync(string userId, MerchantApplicationInput input, CancellationToken cancellationToken = default)
    {
        var businessName = (input.BusinessName ?? string.Empty).Trim();
        if (businessName.Length is < 2 or > 200)
        {
            return Result<Guid>.Validation("Business name must be between 2 and 200 characters.");
        }

        var contactEmail = Normalize(input.ContactEmail);
        var contactPhone = Normalize(input.ContactPhone);
        var now = clock.UtcNow;

        var profile = await db.MerchantProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            var slug = await GenerateUniqueSlugAsync(businessName, cancellationToken);
            profile = new MerchantProfile(userId, businessName, slug, now);
            profile.UpdateBusinessDetails(businessName, contactEmail, contactPhone, now);
            db.MerchantProfiles.Add(profile);
        }
        else if (profile.IsEditable)
        {
            profile.UpdateBusinessDetails(businessName, contactEmail, contactPhone, now);
        }
        else
        {
            return Result<Guid>.Conflict(
                $"This merchant application is {profile.VerificationStatus} and can no longer be edited.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(profile.Id);
    }

    public async Task<Result<Guid>> AddDocumentAsync(string userId, AddVerificationDocumentInput input, CancellationToken cancellationToken = default)
    {
        var metadata = VerificationDocumentValidator.ValidateMetadata(input, _options);
        if (metadata.Failed)
        {
            return Result<Guid>.From(metadata);
        }

        var profile = await db.MerchantProfiles
            .Include(p => p.Documents)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Result<Guid>.Validation("Save your business details before attaching documents.");
        }

        if (!profile.IsEditable)
        {
            return Result<Guid>.Conflict(
                $"This merchant application is {profile.VerificationStatus} and can no longer be edited.");
        }

        if (profile.ActiveDocuments.Count() >= _options.MaxDocumentsPerApplication)
        {
            return Result<Guid>.Validation(
                $"An application can hold at most {_options.MaxDocumentsPerApplication} documents.");
        }

        // Buffer the upload once so the byte signature can be checked before anything is
        // stored, and so the true length (not a client-reported one) is recorded.
        using var buffer = new MemoryStream();
        await input.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            return Result<Guid>.Validation("The file is empty.");
        }

        if (buffer.Length > _options.MaxDocumentBytes)
        {
            return Result<Guid>.Validation(
                $"The file exceeds the {VerificationDocumentValidator.MaxMegabytes(_options)} MB limit.");
        }

        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, VerificationDocumentValidator.SignatureProbeBytes));
        var signature = VerificationDocumentValidator.ValidateSignature(header, input.ContentType);
        if (signature.Failed)
        {
            return Result<Guid>.From(signature);
        }

        buffer.Position = 0;
        var objectKey = await fileStorage.SaveAsync(
            VerificationContainer, buffer, input.OriginalFileName, cancellationToken);

        try
        {
            var document = profile.AddDocument(
                input.DocumentType,
                objectKey,
                SafeFileName(input.OriginalFileName),
                input.ContentType.Trim().ToLowerInvariant(),
                buffer.Length,
                clock.UtcNow);

            await db.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(document.Id);
        }
        catch (Exception ex) when (ex is DbUpdateException or DomainException)
        {
            // The bytes were stored but the metadata row failed: don't leave an orphan.
            await TryDeleteAsync(objectKey, cancellationToken);
            logger.LogError(ex, "Failed to persist verification document metadata for merchant {MerchantId}", profile.Id);
            return Result<Guid>.Conflict("The document could not be saved. Please try again.");
        }
    }

    public async Task<Result> RemoveDocumentAsync(string userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .Include(p => p.Documents)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Result.NotFound("No merchant application was found.");
        }

        if (!profile.IsEditable)
        {
            return Result.Conflict(
                $"This merchant application is {profile.VerificationStatus} and can no longer be edited.");
        }

        try
        {
            profile.RemoveDocument(documentId, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.NotFound(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SubmitForReviewAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .Include(p => p.Documents)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return Result.NotFound("Save your business details and attach a document before submitting.");
        }

        try
        {
            profile.SubmitForReview(clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Validation(ex.Message);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("Your application changed in another tab. Reload it and try again.");
        }

        logger.LogInformation("Merchant application {MerchantId} submitted for review", profile.Id);
        return Result.Success();
    }

    public Task<bool> IsApprovedMerchantAsync(string userId, CancellationToken cancellationToken = default) =>
        db.MerchantProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.VerificationStatus == MerchantVerificationStatus.Approved, cancellationToken);

    public async Task<IReadOnlyList<MerchantQueueItem>> GetQueueAsync(MerchantQueueFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.MerchantProfiles.AsNoTracking();

        query = filter switch
        {
            MerchantQueueFilter.PendingReview => query.Where(p => p.VerificationStatus == MerchantVerificationStatus.PendingReview),
            MerchantQueueFilter.Approved => query.Where(p => p.VerificationStatus == MerchantVerificationStatus.Approved),
            MerchantQueueFilter.Rejected => query.Where(p => p.VerificationStatus == MerchantVerificationStatus.Rejected),
            MerchantQueueFilter.Suspended => query.Where(p => p.VerificationStatus == MerchantVerificationStatus.Suspended),
            _ => query,
        };

        return await query
            .OrderBy(p => p.VerificationStatus == MerchantVerificationStatus.PendingReview ? 0 : 1)
            .ThenBy(p => p.SubmittedAtUtc ?? p.CreatedAtUtc)
            .Select(p => new MerchantQueueItem(
                p.Id,
                p.BusinessName,
                p.VerificationStatus,
                p.SubmittedAtUtc,
                p.CreatedAtUtc,
                p.Documents.Count(d => d.IsActive)))
            .ToListAsync(cancellationToken);
    }

    public async Task<MerchantReviewDetail?> GetForReviewAsync(Guid merchantProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Include(p => p.Documents)
            .SingleOrDefaultAsync(p => p.Id == merchantProfileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        return new MerchantReviewDetail(
            profile.Id,
            profile.UserId,
            profile.BusinessName,
            profile.ContactEmail,
            profile.ContactPhone,
            profile.PublicSlug,
            profile.VerificationStatus,
            profile.CreatedAtUtc,
            profile.SubmittedAtUtc,
            profile.ReviewedAtUtc,
            profile.ReviewedByAdminId,
            profile.RejectionReason,
            MapDocuments(profile));
    }

    public Task<Result> ApproveAsync(string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default) =>
        DecideAsync(
            adminUserId,
            merchantProfileId,
            (profile, now) => profile.Approve(adminUserId, now),
            AdminActionType.MerchantApproved,
            notes: null,
            grantMerchantRole: true,
            cancellationToken);

    public Task<Result> RejectAsync(string adminUserId, Guid merchantProfileId, string reason, CancellationToken cancellationToken = default)
    {
        if (ValidateReason(reason, "rejection") is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId,
            merchantProfileId,
            (profile, now) => profile.Reject(adminUserId, reason, now),
            AdminActionType.MerchantRejected,
            notes: reason.Trim(),
            grantMerchantRole: false,
            cancellationToken);
    }

    public Task<Result> SuspendAsync(string adminUserId, Guid merchantProfileId, string reason, CancellationToken cancellationToken = default)
    {
        if (ValidateReason(reason, "suspension") is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId,
            merchantProfileId,
            (profile, now) => profile.Suspend(adminUserId, reason, now),
            AdminActionType.MerchantSuspended,
            notes: reason.Trim(),
            grantMerchantRole: false,
            cancellationToken);
    }

    public Task<Result> ReinstateAsync(string adminUserId, Guid merchantProfileId, CancellationToken cancellationToken = default) =>
        DecideAsync(
            adminUserId,
            merchantProfileId,
            (profile, now) => profile.Reinstate(adminUserId, now),
            AdminActionType.MerchantReinstated,
            notes: null,
            grantMerchantRole: true,
            cancellationToken);

    public async Task<Result<StoredFileContent>> OpenVerificationDocumentAsync(string adminUserId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.MerchantVerificationDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            return Result<StoredFileContent>.NotFound("The document was not found.");
        }

        var stream = await fileStorage.OpenReadAsync(document.StorageObjectKey, cancellationToken);
        if (stream is null)
        {
            logger.LogError("Verification document {DocumentId} has no backing file at {ObjectKey}", documentId, document.StorageObjectKey);
            return Result<StoredFileContent>.NotFound("The stored file is no longer available.");
        }

        db.AdminActionLogs.Add(new AdminActionLog(
            adminUserId,
            AdminActionType.MerchantVerificationDocumentAccessed,
            nameof(MerchantVerificationDocument),
            documentId.ToString(),
            $"merchantProfileId={document.MerchantProfileId}; file={document.OriginalFileName}",
            clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return Result<StoredFileContent>.Success(
            new StoredFileContent(stream, document.ContentType, document.OriginalFileName));
    }

    private async Task<Result> DecideAsync(
        string adminUserId,
        Guid merchantProfileId,
        Action<MerchantProfile, DateTime> transition,
        AdminActionType actionType,
        string? notes,
        bool grantMerchantRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Result.Forbidden();
        }

        var profile = await db.MerchantProfiles
            .SingleOrDefaultAsync(p => p.Id == merchantProfileId, cancellationToken);

        if (profile is null)
        {
            return Result.NotFound("The merchant application was not found.");
        }

        try
        {
            transition(profile, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        db.AdminActionLogs.Add(new AdminActionLog(
            adminUserId,
            actionType,
            MerchantProfileTargetType,
            profile.Id.ToString(),
            notes,
            clock.UtcNow));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another administrator decided this application first. Nothing was persisted,
            // so the Merchant role grant below is also skipped.
            return Result.Conflict(
                "This application was updated by another administrator. Reload it and try again.");
        }

        if (grantMerchantRole)
        {
            try
            {
                await userRoles.AddToRoleAsync(profile.UserId, FaedRoles.Merchant, cancellationToken);
            }
            catch (Exception ex)
            {
                // The decision is already persisted and audited; a role-sync failure must
                // not roll it back. The ApprovedMerchant policy checks status, not role.
                logger.LogError(ex, "Failed to grant the Merchant role to user {UserId} after {Action}", profile.UserId, actionType);
            }
        }

        logger.LogInformation("Admin {AdminId} performed {Action} on merchant {MerchantId}", adminUserId, actionType, profile.Id);
        return Result.Success();
    }

    private static Result ValidateReason(string? reason, string kind)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Validation($"A {kind} reason is required.");
        }

        if (reason.Trim().Length > MerchantProfile.MaxDecisionReasonLength)
        {
            return Result.Validation(
                $"The {kind} reason must be {MerchantProfile.MaxDecisionReasonLength} characters or fewer.");
        }

        return Result.Success();
    }

    private async Task<string> GenerateUniqueSlugAsync(string businessName, CancellationToken cancellationToken)
    {
        var baseSlug = MerchantSlug.Slugify(businessName);
        var candidate = baseSlug;
        var suffix = 2;

        while (await db.MerchantProfiles.AsNoTracking().AnyAsync(p => p.PublicSlug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private async Task TryDeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(objectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up orphaned verification file {ObjectKey}", objectKey);
        }
    }

    private static MerchantApplicationView MapApplication(MerchantProfile profile) => new(
        profile.Id,
        profile.BusinessName,
        profile.ContactEmail,
        profile.ContactPhone,
        profile.PublicSlug,
        profile.VerificationStatus,
        profile.IsEditable,
        profile.CanSell,
        profile.SubmittedAtUtc,
        profile.ReviewedAtUtc,
        profile.RejectionReason,
        MapDocuments(profile));

    private static IReadOnlyList<VerificationDocumentView> MapDocuments(MerchantProfile profile) =>
        profile.Documents
            .Where(d => d.IsActive)
            .OrderBy(d => d.UploadedAtUtc)
            .Select(d => new VerificationDocumentView(
                d.Id, d.DocumentType, d.OriginalFileName, d.ContentType, d.SizeBytes, d.UploadedAtUtc))
            .ToList();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SafeFileName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName ?? string.Empty).Trim();
        return string.IsNullOrEmpty(name) ? "document" : name;
    }
}
