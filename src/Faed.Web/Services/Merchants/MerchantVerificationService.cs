using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Authorization;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models;
using Faed.Web.Models.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Merchants;

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
    private const string MerchantProfilePublicSlugIndex = "IX_MerchantProfiles_PublicSlug";
    private const string MerchantProfileUserIdIndex = "IX_MerchantProfiles_UserId";
    private const int MaxCreateAttempts = 3;

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
            return await CreateDraftAsync(
                userId,
                businessName,
                contactEmail,
                contactPhone,
                now,
                cancellationToken);
        }

        if (profile.IsEditable)
        {
            profile.UpdateBusinessDetails(businessName, contactEmail, contactPhone, now);
        }
        else
        {
            return Result<Guid>.Conflict(
                $"This merchant application is {profile.VerificationStatus} and can no longer be edited.");
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Conflict("Your application changed in another tab. Reload it and try again.");
        }

        return Result<Guid>.Success(profile.Id);
    }

    private async Task<Result<Guid>> CreateDraftAsync(
        string userId,
        string businessName,
        string? contactEmail,
        string? contactPhone,
        DateTime now,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var slug = await GenerateUniqueSlugAsync(businessName, cancellationToken);
            var profile = new MerchantProfile(userId, businessName, slug, now);
            profile.UpdateBusinessDetails(businessName, contactEmail, contactPhone, now);
            db.MerchantProfiles.Add(profile);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Result<Guid>.Success(profile.Id);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, MerchantProfileUserIdIndex))
            {
                // A second request for this user's first application committed after the
                // initial lookup. Remove our failed Added entity before checking the winner.
                db.MerchantProfiles.Remove(profile);
                var existing = await db.MerchantProfiles
                    .AsNoTracking()
                    .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (existing is null)
                {
                    // The named unique index could not have rejected this row without a
                    // conflicting value. Preserve unexpected provider behaviour for diagnosis.
                    throw;
                }

                return existing.IsEditable
                    ? Result<Guid>.Conflict(
                        "Your application was created in another tab. Reload it and try again.")
                    : Result<Guid>.Conflict(
                        $"This merchant application is {existing.VerificationStatus} and can no longer be edited.");
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, MerchantProfilePublicSlugIndex))
            {
                // Slug availability is necessarily check-then-insert. Detach the failed row,
                // regenerate against committed data, and retry within a small fixed budget.
                db.MerchantProfiles.Remove(profile);
                if (attempt == MaxCreateAttempts)
                {
                    return Result<Guid>.Conflict(
                        "Another merchant claimed this public address while you were saving. Please try again.");
                }
            }
        }

        throw new InvalidOperationException("The merchant draft creation retry loop exited unexpectedly.");
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

        var payload = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var content = VerificationDocumentValidator.ValidatePayload(payload, input.ContentType);
        if (content.Failed)
        {
            return Result<Guid>.From(content);
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("Your application changed in another tab. Reload it and try again.");
        }

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
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            // Private verification documents are admin-only; never trust the caller alone
            // (docs/08-SECURITY-AND-PRIVACY.md §2-3).
            return Result<StoredFileContent>.Forbidden();
        }

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
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            // Defence in depth: the MVC route is already behind the AdminOnly policy, but the
            // service contract must not trust its caller (docs/08-SECURITY-AND-PRIVACY.md §2).
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

        // The status change, its audit entry and the Merchant role grant either all commit
        // or none do: a permanent role-sync failure must not leave an approved profile that
        // can never be re-approved (AGENTS.md §7).
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            if (grantMerchantRole)
            {
                await userRoles.AddToRoleAsync(profile.UserId, FaedRoles.Merchant, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another administrator decided this application first. The transaction is rolled
            // back on dispose, so nothing was persisted.
            return Result.Conflict(
                "This application was updated by another administrator. Reload it and try again.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete {Action} on merchant {MerchantId}; the change was rolled back", actionType, profile.Id);
            return Result.Conflict("The decision could not be completed. Reload the application and try again.");
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

    private static bool IsUniqueIndexViolation(DbUpdateException exception, string indexName)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
            {
                continue;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number is 2601 or 2627
                    && error.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
