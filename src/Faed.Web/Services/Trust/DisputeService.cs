using Faed.Web.Authorization;
using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Models.Identity;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Common;
using Faed.Web.Services.Listings;
using Faed.Web.Services.Merchants;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Trust;

/// <inheritdoc />
public sealed class DisputeService(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    IUserRoleService userRoles,
    IClock clock,
    IOptions<TrustOptions> options,
    ILogger<DisputeService> logger) : IDisputeService
{
    private const string EvidenceContainer = "dispute-evidence";
    private const string DisputeTargetType = nameof(Dispute);
    private const string ActiveDisputeIndex = "IX_Disputes_ActiveTransactionKey_Unique";

    private const string DuplicateActiveDisputeMessage =
        "There is already an open dispute for this transaction.";

    private readonly TrustOptions _options = options.Value;

    // ---- File a dispute -----------------------------------------------------------

    public async Task<Result<Guid>> FileDisputeAsync(
        string userId, FileDisputeInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<Guid>.Forbidden("Sign in to raise a dispute.");
        }

        // Administrators resolve disputes; they do not file them
        // (docs/16-PERMISSIONS-MATRIX.md "File eligible dispute — Admin ❌").
        if (await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken))
        {
            return Result<Guid>.Forbidden("Administrators cannot raise disputes.");
        }

        if (!Enum.IsDefined(input.ReasonCode))
        {
            return Result<Guid>.Validation("Choose a reason for the dispute.");
        }

        var description = (input.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            return Result<Guid>.Validation("Describe what went wrong.");
        }

        if (description.Length > Dispute.MaxDescriptionLength)
        {
            return Result<Guid>.Validation(
                $"The description must be {Dispute.MaxDescriptionLength} characters or fewer.");
        }

        var files = input.Evidence ?? [];
        if (files.Count > _options.MaxEvidenceFilesPerDispute)
        {
            return Result<Guid>.Validation(
                $"Attach at most {_options.MaxEvidenceFilesPerDispute} evidence files.");
        }

        var context = await ResolveContextAsync(input.TransactionType, input.TransactionId, cancellationToken);
        if (context is null)
        {
            return Result<Guid>.NotFound("That transaction was not found.");
        }

        if (!context.ParticipantUserIds.Contains(userId))
        {
            // A non-participant learns nothing — same as "not found" (docs/08-SECURITY-AND-PRIVACY.md §9).
            return Result<Guid>.NotFound("That transaction was not found.");
        }

        if (!context.AllowsNewDispute)
        {
            return Result<Guid>.Validation(context.DisputeBlockedReason
                ?? "This transaction cannot be disputed right now.");
        }

        // Fast, friendly path. The authoritative guard against two filings racing is the
        // filtered unique index on Dispute.ActiveTransactionKey — a concurrent second insert
        // is rejected by the database and translated below (docs/03-BUSINESS-RULES.md §14).
        var activeKey = Dispute.ActiveKeyFor(input.TransactionType, input.TransactionId);
        if (await db.Disputes.AnyAsync(d => d.ActiveTransactionKey == activeKey, cancellationToken))
        {
            return Result<Guid>.Conflict(DuplicateActiveDisputeMessage);
        }

        // Buffer and validate every file before anything is stored, so a bad file rejects the
        // whole request rather than leaving partial evidence (docs/08-SECURITY-AND-PRIVACY.md §4).
        var buffered = new List<(byte[] Bytes, string FileName, string ContentType)>();
        foreach (var file in files)
        {
            var validation = await ValidateEvidenceAsync(file, cancellationToken);
            if (validation.Failed)
            {
                return Result<Guid>.From(validation);
            }

            buffered.Add(validation.Value);
        }

        var dispute = new Dispute(
            input.TransactionType == TrustTransactionType.B2COrder ? input.TransactionId : null,
            input.TransactionType == TrustTransactionType.B2BDeal ? input.TransactionId : null,
            userId,
            input.ReasonCode,
            description,
            clock.UtcNow);

        var storedKeys = new List<string>();
        try
        {
            foreach (var (bytes, fileName, contentType) in buffered)
            {
                await using var stream = new MemoryStream(bytes, writable: false);
                var key = await fileStorage.SaveAsync(EvidenceContainer, stream, fileName, cancellationToken);
                storedKeys.Add(key);
                dispute.AddEvidence(userId, key, fileName, contentType, bytes.Length, clock.UtcNow);
            }

            db.Disputes.Add(dispute);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, ActiveDisputeIndex))
        {
            // A concurrent filing won the race to the unique index.
            foreach (var key in storedKeys)
            {
                await TryDeleteAsync(key, cancellationToken);
            }

            return Result<Guid>.Conflict(DuplicateActiveDisputeMessage);
        }
        catch (Exception ex) when (ex is DbUpdateException or DomainException)
        {
            foreach (var key in storedKeys)
            {
                await TryDeleteAsync(key, cancellationToken);
            }

            logger.LogError(ex, "Failed to persist dispute for user {UserId} on {Type} {TransactionId}",
                userId, input.TransactionType, input.TransactionId);
            return Result<Guid>.Conflict("The dispute could not be saved. Please try again.");
        }

        logger.LogInformation(
            "User {UserId} filed dispute {DisputeId} on {Type} {TransactionId}",
            userId, dispute.Id, input.TransactionType, input.TransactionId);
        return Result<Guid>.Success(dispute.Id);
    }

    public async Task<Result> AddEvidenceAsync(
        string userId, Guid disputeId, IReadOnlyList<DisputeEvidenceUpload> files, CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
        {
            return Result.Validation("Choose at least one file to attach.");
        }

        var dispute = await db.Disputes
            .Include(d => d.Evidence)
            .SingleOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return Result.NotFound("That dispute was not found.");
        }

        var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
        if (context is null || !context.ParticipantUserIds.Contains(userId))
        {
            return Result.NotFound("That dispute was not found.");
        }

        if (!dispute.AcceptsEvidence)
        {
            return Result.Conflict("This dispute is closed and can no longer take new evidence.");
        }

        if (dispute.Evidence.Count + files.Count > _options.MaxEvidenceFilesPerDispute)
        {
            return Result.Validation(
                $"A dispute can hold at most {_options.MaxEvidenceFilesPerDispute} evidence files.");
        }

        var buffered = new List<(byte[] Bytes, string FileName, string ContentType)>();
        foreach (var file in files)
        {
            var validation = await ValidateEvidenceAsync(file, cancellationToken);
            if (validation.Failed)
            {
                return Result.From(validation);
            }

            buffered.Add(validation.Value);
        }

        var storedKeys = new List<string>();
        try
        {
            foreach (var (bytes, fileName, contentType) in buffered)
            {
                await using var stream = new MemoryStream(bytes, writable: false);
                var key = await fileStorage.SaveAsync(EvidenceContainer, stream, fileName, cancellationToken);
                storedKeys.Add(key);
                dispute.AddEvidence(userId, key, fileName, contentType, bytes.Length, clock.UtcNow);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbUpdateException or DomainException)
        {
            foreach (var key in storedKeys)
            {
                await TryDeleteAsync(key, cancellationToken);
            }

            logger.LogError(ex, "Failed to add evidence to dispute {DisputeId}", disputeId);
            return Result.Conflict("The evidence could not be saved. Please try again.");
        }

        return Result.Success();
    }

    // ---- Participant reads -----------------------------------------------------

    public async Task<PagedResult<DisputeSummaryView>> GetMyDisputesAsync(
        string userId, int page = 1, CancellationToken cancellationToken = default)
    {
        var mine = await MyDisputesQueryAsync(userId, cancellationToken);
        page = Paging.NormalizePage(page);

        var totalCount = await mine.CountAsync(cancellationToken);
        var disputes = await mine
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Skip((page - 1) * Paging.DefaultPageSize)
            .Take(Paging.DefaultPageSize)
            .ToListAsync(cancellationToken);

        var summaries = await ToParticipantSummariesAsync(disputes, userId, cancellationToken);
        return new PagedResult<DisputeSummaryView>(summaries, totalCount, page, Paging.DefaultPageSize);
    }

    public async Task<IReadOnlyList<DisputeSummaryView>> GetDisputesForTransactionAsync(
        string userId, TrustTransactionType transactionType, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var mine = await MyDisputesQueryAsync(userId, cancellationToken);
        mine = transactionType == TrustTransactionType.B2COrder
            ? mine.Where(d => d.OrderId == transactionId)
            : mine.Where(d => d.B2BDealId == transactionId);

        var disputes = await mine.OrderByDescending(d => d.UpdatedAtUtc).ToListAsync(cancellationToken);
        return await ToParticipantSummariesAsync(disputes, userId, cancellationToken);
    }

    private async Task<IQueryable<Dispute>> MyDisputesQueryAsync(string userId, CancellationToken cancellationToken)
    {
        var myMerchantId = await ResolveMerchantIdAsync(userId, cancellationToken);

        var orderIds = await db.Orders
            .AsNoTracking()
            .Where(o => o.BuyerUserId == userId || (myMerchantId != null && o.MerchantProfileId == myMerchantId))
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var dealIds = myMerchantId is null
            ? []
            : await db.B2BDeals
                .AsNoTracking()
                .Where(d => d.SellingMerchantProfileId == myMerchantId || d.BuyingMerchantProfileId == myMerchantId)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

        return db.Disputes
            .AsNoTracking()
            .Where(d => d.RaisedByUserId == userId
                || (d.OrderId != null && orderIds.Contains(d.OrderId.Value))
                || (d.B2BDealId != null && dealIds.Contains(d.B2BDealId.Value)));
    }

    private async Task<List<DisputeSummaryView>> ToParticipantSummariesAsync(
        IReadOnlyList<Dispute> disputes, string userId, CancellationToken cancellationToken)
    {
        var summaries = new List<DisputeSummaryView>(disputes.Count);
        foreach (var dispute in disputes)
        {
            var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
            if (context is null)
            {
                continue;
            }

            summaries.Add(new DisputeSummaryView(
                dispute.Id,
                dispute.Status,
                dispute.ReasonCode,
                dispute.TransactionType,
                TransactionIdOf(dispute),
                context.Reference,
                dispute.RaisedByUserId == userId ? context.CounterpartyNameFor(userId) : context.RaisedByNameFor(dispute.RaisedByUserId),
                dispute.CreatedAtUtc,
                dispute.UpdatedAtUtc));
        }

        return summaries;
    }

    public async Task<DisputeDetailView?> GetMyDisputeAsync(
        string userId, Guid disputeId, CancellationToken cancellationToken = default)
    {
        var dispute = await db.Disputes
            .AsNoTracking()
            .Include(d => d.Evidence)
            .SingleOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return null;
        }

        var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
        var isAdmin = await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken);
        if (context is null || (!context.ParticipantUserIds.Contains(userId) && !isAdmin))
        {
            return null;
        }

        return new DisputeDetailView(
            dispute.Id,
            dispute.Status,
            dispute.ReasonCode,
            dispute.Description,
            dispute.AdminResolution,
            dispute.TransactionType,
            TransactionIdOf(dispute),
            context.Reference,
            context.ListingSlug,
            context.RaisedByNameFor(dispute.RaisedByUserId),
            dispute.RaisedByUserId == userId,
            context.SellingMerchantName,
            context.BuyerName,
            dispute.CreatedAtUtc,
            dispute.ResolvedAtUtc,
            dispute.AcceptsEvidence && context.ParticipantUserIds.Contains(userId),
            MapEvidence(dispute, userId));
    }

    public async Task<Result<StoredFileContent>> OpenEvidenceAsync(
        string userId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<StoredFileContent>.Forbidden();
        }

        var evidence = await db.DisputeEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == evidenceId, cancellationToken);

        if (evidence is null)
        {
            return Result<StoredFileContent>.NotFound("The evidence file was not found.");
        }

        var dispute = await db.Disputes
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == evidence.DisputeId, cancellationToken);
        if (dispute is null)
        {
            return Result<StoredFileContent>.NotFound("The evidence file was not found.");
        }

        var isAdmin = await userRoles.IsInRoleAsync(userId, FaedRoles.Admin, cancellationToken);
        if (!isAdmin)
        {
            var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
            if (context is null || !context.ParticipantUserIds.Contains(userId))
            {
                // Dispute evidence is private to the participants and admins. A non-participant
                // gets exactly the response a non-existent id gets — "not found" — so guessing
                // ids never reveals which evidence exists (docs/08-SECURITY-AND-PRIVACY.md §3, §9).
                return Result<StoredFileContent>.NotFound("The evidence file was not found.");
            }
        }

        var stream = await fileStorage.OpenReadAsync(evidence.StorageObjectKey, cancellationToken);
        if (stream is null)
        {
            logger.LogError("Dispute evidence {EvidenceId} has no backing file at {ObjectKey}",
                evidenceId, evidence.StorageObjectKey);
            return Result<StoredFileContent>.NotFound("The stored file is no longer available.");
        }

        if (isAdmin)
        {
            db.AdminActionLogs.Add(new AdminActionLog(
                userId,
                AdminActionType.DisputeEvidenceAccessed,
                nameof(DisputeEvidence),
                evidenceId.ToString(),
                $"disputeId={dispute.Id}; file={evidence.OriginalFileName}",
                clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<StoredFileContent>.Success(
            new StoredFileContent(stream, evidence.ContentType, evidence.OriginalFileName));
    }

    // ---- Admin reads / actions ------------------------------------------------

    public async Task<PagedResult<DisputeSummaryView>> GetQueueAsync(
        DisputeQueueFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = db.Disputes.AsNoTracking();
        query = filter switch
        {
            DisputeQueueFilter.Active => query.Where(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview),
            DisputeQueueFilter.Open => query.Where(d => d.Status == DisputeStatus.Open),
            DisputeQueueFilter.UnderReview => query.Where(d => d.Status == DisputeStatus.UnderReview),
            DisputeQueueFilter.Resolved => query.Where(d => d.Status == DisputeStatus.Resolved),
            DisputeQueueFilter.Rejected => query.Where(d => d.Status == DisputeStatus.Rejected),
            _ => query,
        };

        page = Paging.NormalizePage(page);
        var totalCount = await query.CountAsync(cancellationToken);
        var disputes = await query
            .OrderBy(d => d.Status == DisputeStatus.Open ? 0 : d.Status == DisputeStatus.UnderReview ? 1 : 2)
            .ThenBy(d => d.CreatedAtUtc)
            .Skip((page - 1) * Paging.AdminPageSize)
            .Take(Paging.AdminPageSize)
            .ToListAsync(cancellationToken);

        var summaries = new List<DisputeSummaryView>(disputes.Count);
        foreach (var dispute in disputes)
        {
            var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
            if (context is null)
            {
                continue;
            }

            summaries.Add(new DisputeSummaryView(
                dispute.Id,
                dispute.Status,
                dispute.ReasonCode,
                dispute.TransactionType,
                TransactionIdOf(dispute),
                context.Reference,
                context.SellingMerchantName,
                dispute.CreatedAtUtc,
                dispute.UpdatedAtUtc));
        }

        return new PagedResult<DisputeSummaryView>(summaries, totalCount, page, Paging.AdminPageSize);
    }

    public Task<int> GetOpenDisputeCountAsync(CancellationToken cancellationToken = default) =>
        db.Disputes.AsNoTracking().CountAsync(
            d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview, cancellationToken);

    public async Task<AdminDisputeDetailView?> GetForReviewAsync(
        Guid disputeId, CancellationToken cancellationToken = default)
    {
        var dispute = await db.Disputes
            .AsNoTracking()
            .Include(d => d.Evidence)
            .SingleOrDefaultAsync(d => d.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return null;
        }

        var context = await ResolveContextAsync(dispute.TransactionType, TransactionIdOf(dispute), cancellationToken);
        if (context is null)
        {
            return null;
        }

        return new AdminDisputeDetailView(
            dispute.Id,
            dispute.Status,
            dispute.ReasonCode,
            dispute.Description,
            dispute.AdminResolution,
            dispute.ResolvedByAdminId,
            dispute.TransactionType,
            TransactionIdOf(dispute),
            context.Reference,
            context.RaisedByNameFor(dispute.RaisedByUserId),
            dispute.RaisedByUserId,
            context.SellingMerchantName,
            context.BuyerName,
            context.TransactionTotal,
            dispute.CreatedAtUtc,
            dispute.ResolvedAtUtc,
            MapEvidence(dispute, adminViewerUserId: null));
    }

    public Task<Result> StartReviewAsync(
        string adminUserId, Guid disputeId, CancellationToken cancellationToken = default) =>
        DecideAsync(
            adminUserId, disputeId,
            (dispute, now) => dispute.StartReview(adminUserId, now),
            AdminActionType.DisputeReviewStarted, notes: null, cancellationToken);

    public Task<Result> ResolveAsync(
        string adminUserId, Guid disputeId, string resolution, CancellationToken cancellationToken = default)
    {
        if (ValidateResolution(resolution) is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId, disputeId,
            (dispute, now) => dispute.Resolve(adminUserId, resolution, now),
            AdminActionType.DisputeResolved, notes: resolution.Trim(), cancellationToken);
    }

    public Task<Result> RejectAsync(
        string adminUserId, Guid disputeId, string resolution, CancellationToken cancellationToken = default)
    {
        if (ValidateResolution(resolution) is { Failed: true } invalid)
        {
            return Task.FromResult(invalid);
        }

        return DecideAsync(
            adminUserId, disputeId,
            (dispute, now) => dispute.Reject(adminUserId, resolution, now),
            AdminActionType.DisputeRejected, notes: resolution.Trim(), cancellationToken);
    }

    // ---- Internals -----------------------------------------------------------

    private async Task<Result> DecideAsync(
        string adminUserId,
        Guid disputeId,
        Action<Dispute, DateTime> transition,
        AdminActionType actionType,
        string? notes,
        CancellationToken cancellationToken)
    {
        // Defence in depth: the MVC route is behind AdminOnly, but the service contract must
        // not trust its caller (docs/08-SECURITY-AND-PRIVACY.md §2,
        // docs/17-DATA-INVARIANTS.md "Resolution actor must be Admin").
        if (!await userRoles.IsInRoleAsync(adminUserId, FaedRoles.Admin, cancellationToken))
        {
            return Result.Forbidden();
        }

        var dispute = await db.Disputes.SingleOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
        if (dispute is null)
        {
            return Result.NotFound("That dispute was not found.");
        }

        try
        {
            transition(dispute, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Conflict(ex.Message);
        }

        db.AdminActionLogs.Add(new AdminActionLog(
            adminUserId, actionType, DisputeTargetType, dispute.Id.ToString(), notes, clock.UtcNow));

        // The status change and its audit entry commit together or not at all (AGENTS.md §7).
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("Another administrator updated this dispute. Reload it and try again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to record {Action} on dispute {DisputeId}", actionType, dispute.Id);
            return Result.Conflict("The decision could not be saved. Reload the dispute and try again.");
        }

        logger.LogInformation("Admin {AdminId} performed {Action} on dispute {DisputeId}", adminUserId, actionType, dispute.Id);
        return Result.Success();
    }

    private static Result ValidateResolution(string? resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution))
        {
            return Result.Validation("Record an outcome for the dispute.");
        }

        return resolution.Trim().Length > Dispute.MaxResolutionLength
            ? Result.Validation($"The outcome must be {Dispute.MaxResolutionLength} characters or fewer.")
            : Result.Success();
    }

    private async Task<Result<(byte[] Bytes, string FileName, string ContentType)>> ValidateEvidenceAsync(
        DisputeEvidenceUpload file, CancellationToken cancellationToken)
    {
        var metadata = ListingImageValidator.ValidateMetadata(
            file.OriginalFileName, file.ContentType, file.LengthBytes,
            _options.MaxEvidenceBytes, ListingImageValidator.EvidenceContentTypes);
        if (metadata.Failed)
        {
            return Result<(byte[], string, string)>.From(metadata);
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            return Result<(byte[], string, string)>.Validation("The file is empty.");
        }

        if (buffer.Length > _options.MaxEvidenceBytes)
        {
            return Result<(byte[], string, string)>.Validation(
                $"The file exceeds the {ListingImageValidator.Megabytes(_options.MaxEvidenceBytes)} MB limit.");
        }

        var bytes = buffer.ToArray();
        var payload = ListingImageValidator.ValidatePayload(bytes, file.ContentType);
        if (payload.Failed)
        {
            return Result<(byte[], string, string)>.From(payload);
        }

        var safeName = Path.GetFileName(file.OriginalFileName ?? string.Empty).Trim();
        return Result<(byte[], string, string)>.Success((
            bytes,
            string.IsNullOrEmpty(safeName) ? "evidence" : safeName,
            (file.ContentType ?? string.Empty).Trim().ToLowerInvariant()));
    }

    private IReadOnlyList<DisputeEvidenceView> MapEvidence(Dispute dispute, string? adminViewerUserId) =>
        dispute.Evidence
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => new DisputeEvidenceView(
                e.Id, e.OriginalFileName, e.ContentType, e.SizeBytes, e.CreatedAtUtc,
                adminViewerUserId is not null && e.UploadedByUserId == adminViewerUserId))
            .ToList();

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
            logger.LogWarning(ex, "Failed to clean up orphaned dispute evidence file {ObjectKey}", objectKey);
        }
    }

    private Task<Guid?> ResolveMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private static Guid TransactionIdOf(Dispute dispute) =>
        dispute.OrderId ?? dispute.B2BDealId!.Value;

    private async Task<TransactionContext?> ResolveContextAsync(
        TrustTransactionType type, Guid transactionId, CancellationToken cancellationToken)
    {
        if (type == TrustTransactionType.B2COrder)
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .SingleOrDefaultAsync(o => o.Id == transactionId, cancellationToken);
            if (order is null)
            {
                return null;
            }

            var merchant = await db.MerchantProfiles
                .AsNoTracking()
                .Where(m => m.Id == order.MerchantProfileId)
                .Select(m => new { m.UserId, m.BusinessName })
                .SingleOrDefaultAsync(cancellationToken);
            if (merchant is null)
            {
                return null;
            }

            var listingId = order.Items.Select(i => i.ListingId).FirstOrDefault();
            var slug = listingId == Guid.Empty
                ? null
                : await db.Listings.AsNoTracking()
                    .Where(l => l.Id == listingId).Select(l => l.Slug).SingleOrDefaultAsync(cancellationToken);

            var allowsDispute = order.Status is OrderStatus.Confirmed or OrderStatus.ReadyForPickup
                or OrderStatus.OutForDelivery or OrderStatus.Completed;

            return new TransactionContext
            {
                Reference = $"Order {order.Id.ToString()[..8]}",
                ListingSlug = slug,
                SellingMerchantName = merchant.BusinessName,
                BuyerName = order.ContactName,
                SellingMerchantUserId = merchant.UserId,
                BuyerOrBuyingMerchantUserId = order.BuyerUserId,
                TransactionTotal = order.Total,
                AllowsNewDispute = allowsDispute,
                DisputeBlockedReason = allowsDispute
                    ? null
                    : "A dispute can be raised once the merchant has confirmed the order.",
            };
        }

        var deal = await db.B2BDeals
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == transactionId, cancellationToken);
        if (deal is null)
        {
            return null;
        }

        var merchants = await db.MerchantProfiles
            .AsNoTracking()
            .Where(m => m.Id == deal.SellingMerchantProfileId || m.Id == deal.BuyingMerchantProfileId)
            .Select(m => new { m.Id, m.UserId, m.BusinessName })
            .ToListAsync(cancellationToken);
        var seller = merchants.SingleOrDefault(m => m.Id == deal.SellingMerchantProfileId);
        var buyer = merchants.SingleOrDefault(m => m.Id == deal.BuyingMerchantProfileId);
        if (seller is null || buyer is null)
        {
            return null;
        }

        var negotiation = await db.B2BNegotiations
            .AsNoTracking()
            .Where(n => n.Id == deal.B2BNegotiationId)
            .Select(n => n.ListingId)
            .SingleOrDefaultAsync(cancellationToken);
        var dealSlug = negotiation == Guid.Empty
            ? null
            : await db.Listings.AsNoTracking()
                .Where(l => l.Id == negotiation).Select(l => l.Slug).SingleOrDefaultAsync(cancellationToken);

        var dealAllows = deal.Status != B2BDealStatus.Cancelled;

        return new TransactionContext
        {
            Reference = $"Deal {deal.Id.ToString()[..8]}",
            ListingSlug = dealSlug,
            SellingMerchantName = seller.BusinessName,
            BuyerName = buyer.BusinessName,
            SellingMerchantUserId = seller.UserId,
            BuyerOrBuyingMerchantUserId = buyer.UserId,
            TransactionTotal = deal.TotalSnapshot,
            AllowsNewDispute = dealAllows,
            DisputeBlockedReason = dealAllows ? null : "A cancelled deal cannot be disputed.",
        };
    }

    /// <summary>The participant-facing facts about the order or deal a dispute references.</summary>
    private sealed class TransactionContext
    {
        public required string Reference { get; init; }

        public string? ListingSlug { get; init; }

        public required string SellingMerchantName { get; init; }

        public required string BuyerName { get; init; }

        public required string SellingMerchantUserId { get; init; }

        public required string BuyerOrBuyingMerchantUserId { get; init; }

        public decimal TransactionTotal { get; init; }

        public bool AllowsNewDispute { get; init; }

        public string? DisputeBlockedReason { get; init; }

        public IReadOnlyCollection<string> ParticipantUserIds =>
            [SellingMerchantUserId, BuyerOrBuyingMerchantUserId];

        public string RaisedByNameFor(string userId) =>
            userId == SellingMerchantUserId ? SellingMerchantName : BuyerName;

        public string CounterpartyNameFor(string userId) =>
            userId == SellingMerchantUserId ? BuyerName : SellingMerchantName;
    }
}
