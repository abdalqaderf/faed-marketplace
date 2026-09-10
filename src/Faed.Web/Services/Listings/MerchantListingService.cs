using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;
using Faed.Web.Services.Subscriptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Listings;

/// <inheritdoc />
public sealed class MerchantListingService(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    ISubscriptionService subscriptions,
    IClock clock,
    IOptions<ListingOptions> options,
    ILogger<MerchantListingService> logger) : IMerchantListingService
{
    private const string MediaContainer = "listing-media";
    private const string EvidenceContainer = "listing-evidence";
    private const string ListingSlugIndex = "IX_Listings_Slug";
    private const int MaxSlugAttempts = 5;

    private readonly ListingOptions _options = options.Value;

    public async Task<ListingReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default)
    {
        // Categories, grades and reasons are admin-managed reference data; the form must read
        // them from the database rather than hard-code the launch taxonomy.
        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: true, includeRoot: false, cancellationToken);
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => launchCategoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CatalogChoice(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        // The four condition cards already imply one reason each; the "Add another reason"
        // link only needs to offer the rest.
        var presetReasonCodes = ConditionPresets.All
            .SelectMany(p => p.ReasonCodes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var additionalReasons = (await db.DiscountReasons
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Code, r.Name })
                .ToListAsync(cancellationToken))
            .Where(r => !presetReasonCodes.Contains(r.Code))
            .Select(r => new CatalogChoice(r.Id, r.Name))
            .ToList();

        return new ListingReferenceData(categories, additionalReasons);
    }

    public async Task<PagedResult<MerchantListingListItem>> GetMyListingsAsync(
        string userId, MerchantListingFilter filter, int page = 1, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(userId, cancellationToken);
        if (merchantId is null)
        {
            return PagedResult<MerchantListingListItem>.Empty(Paging.NormalizePage(page), Paging.DefaultPageSize);
        }

        var query = db.Listings.AsNoTracking().Where(l => l.MerchantProfileId == merchantId);

        query = filter switch
        {
            MerchantListingFilter.Draft => query.Where(l => l.Status == ListingStatus.Draft),
            MerchantListingFilter.PendingReview => query.Where(l => l.Status == ListingStatus.PendingReview),
            MerchantListingFilter.Live => query.Where(l =>
                l.Status == ListingStatus.Live || l.Status == ListingStatus.SoldOut),
            MerchantListingFilter.Rejected => query.Where(l => l.Status == ListingStatus.Rejected),
            // "Needs attention" is the merchant's work queue: everything that is not published
            // and not archived is waiting on them.
            MerchantListingFilter.NeedsAttention => query.Where(l =>
                l.Status == ListingStatus.Draft
                || l.Status == ListingStatus.Rejected
                || l.Status == ListingStatus.SoldOut),
            _ => query.Where(l => l.Status != ListingStatus.Archived),
        };

        return await query
            .OrderByDescending(l => l.UpdatedAtUtc)
            .Select(l => new MerchantListingListItem(
                l.Id,
                l.Title,
                l.Status,
                db.Categories.Where(c => c.Id == l.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "—",
                db.ConditionGrades.Where(g => g.Id == l.ConditionGradeId).Select(g => g.Code).FirstOrDefault() ?? "?",
                l.RetailPrice,
                l.Variants.Count,
                l.Variants.Where(v => v.IsActive).Sum(v => (int?)v.AvailableQuantity) ?? 0,
                l.Media.Any(m => m.MediaType == ListingMediaType.Defect),
                l.UpdatedAtUtc,
                l.Moderations
                    .Where(m => m.Status != ListingModerationStatus.Pending)
                    .OrderByDescending(m => m.SubmittedAtUtc)
                    .Select(m => m.ReviewNote)
                    .FirstOrDefault()))
            .ToPagedResultAsync(page, Paging.DefaultPageSize, cancellationToken);
    }

    public async Task<ListingDetailView?> GetMyListingAsync(
        string userId, Guid listingId, CancellationToken cancellationToken = default)
    {
        var merchantId = await ResolveMerchantIdAsync(userId, cancellationToken);
        if (merchantId is null)
        {
            return null;
        }

        var listing = await db.Listings
            .AsNoTracking()
            .WithAggregate()
            .SingleOrDefaultAsync(l => l.Id == listingId && l.MerchantProfileId == merchantId, cancellationToken);

        return listing is null ? null : await listing.ToDetailViewAsync(db, cancellationToken);
    }

    public async Task<Result<Guid>> CreateAsync(
        string userId, ListingDetailsInput input, CancellationToken cancellationToken = default)
    {
        var merchant = await RequireRegisteredMerchantAsync(userId, cancellationToken);
        if (merchant.Failed)
        {
            return Result<Guid>.From(merchant);
        }

        var validation = await ValidateDetailsAsync(input, cancellationToken);
        if (validation.Failed)
        {
            return Result<Guid>.From(validation);
        }

        var now = clock.UtcNow;
        var baseSlug = Slug.Truncate(Slug.Create(input.Title, "listing"), Listing.MaxSlugLength - 8);

        for (var attempt = 1; attempt <= MaxSlugAttempts; attempt++)
        {
            var slug = await NextAvailableSlugAsync(baseSlug, cancellationToken);
            Listing listing;
            try
            {
                listing = new Listing(
                    merchant.Value, input.CategoryId, input.ConditionGradeId,
                    input.Title, slug, input.Description, now);

                ApplyDetails(listing, input, now);
            }
            catch (DomainException ex)
            {
                return Result<Guid>.Validation(ex.Message);
            }

            db.Listings.Add(listing);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Merchant {MerchantId} created listing {ListingId}", merchant.Value, listing.Id);
                return Result<Guid>.Success(listing.Id);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, ListingSlugIndex))
            {
                // Slug availability is necessarily check-then-insert. Detach the failed row and
                // regenerate against committed data within a small fixed budget.
                db.Listings.Remove(listing);
                if (attempt == MaxSlugAttempts)
                {
                    return Result<Guid>.Conflict(
                        "Another listing claimed this address while you were saving. Please try again.");
                }
            }
        }

        throw new InvalidOperationException("The listing slug retry loop exited unexpectedly.");
    }

    public async Task<Result<SaveListingOutcome>> SaveListingAsync(
        string userId, Guid? listingId, ListingFormSubmission submission, CancellationToken cancellationToken = default)
    {
        var merchant = await RequireRegisteredMerchantAsync(userId, cancellationToken);
        if (merchant.Failed)
        {
            return Result<SaveListingOutcome>.From(merchant);
        }

        var resolved = await ResolveConditionAsync(submission, cancellationToken);
        if (resolved.Failed)
        {
            return Result<SaveListingOutcome>.From(resolved);
        }

        var (gradeId, reasonIds) = resolved.Value;

        Listing? existing = null;
        if (listingId is { } id)
        {
            existing = await db.Listings
                .WithAggregate()
                .SingleOrDefaultAsync(l => l.Id == id && l.MerchantProfileId == merchant.Value, cancellationToken);
            if (existing is null)
            {
                return Result<SaveListingOutcome>.NotFound("That listing was not found.");
            }
        }

        // The one-page form no longer shows return policy or included/missing items; preserve
        // whatever an older listing already carries rather than blanking it on every edit.
        var details = new ListingDetailsInput(
            submission.CategoryId, gradeId, submission.Title, submission.Description,
            ReferencePrice: submission.OriginalPrice, RetailPrice: submission.Price,
            ReturnPolicyText: existing?.ReturnPolicyText,
            submission.WarrantyType, submission.WarrantyMonths,
            IncludedItemsText: existing?.IncludedItemsText,
            MissingItemsText: existing?.MissingItemsText,
            reasonIds);

        var validation = await ValidateDetailsAsync(details, cancellationToken);
        if (validation.Failed)
        {
            return Result<SaveListingOutcome>.From(validation);
        }

        // Buffer and store every new upload before touching the aggregate, so a storage
        // failure can never leave a half-built listing. Any stored key is cleaned up if the
        // aggregate rejects the file or the save fails.
        var storedKeys = new List<string>();
        var product = new List<StoredUpload>();
        var defect = new List<StoredUpload>();
        StoredUpload? evidenceFile = null;

        async Task<Result<SaveListingOutcome>> AbortAsync(Result failure)
        {
            foreach (var key in storedKeys)
            {
                await TryDeleteAsync(key, CancellationToken.None);
            }

            return Result<SaveListingOutcome>.From(failure);
        }

        foreach (var (source, sink) in new[]
                 {
                     (submission.NewProductPhotos, product),
                     (submission.NewDefectPhotos, defect),
                 })
        {
            foreach (var photo in source)
            {
                var stored = await BufferValidateAndStoreAsync(
                    MediaContainer, photo.Content, photo.FileName, photo.ContentType,
                    ListingImageValidator.ImageContentTypes, cancellationToken);
                if (stored.Failed)
                {
                    return await AbortAsync(stored);
                }

                storedKeys.Add(stored.Value.ObjectKey);
                sink.Add(stored.Value);
            }
        }

        if (submission.OriginalPriceEvidence?.File is { } file)
        {
            var stored = await BufferValidateAndStoreAsync(
                EvidenceContainer, file.Content, file.FileName, file.ContentType,
                ListingImageValidator.EvidenceContentTypes, cancellationToken);
            if (stored.Failed)
            {
                return await AbortAsync(stored);
            }

            storedKeys.Add(stored.Value.ObjectKey);
            evidenceFile = stored.Value;
        }

        var now = clock.UtcNow;
        var evidence = submission.OriginalPriceEvidence;

        try
        {
            return existing is null
                ? await CreateSubmittedListingAsync(merchant.Value, submission, details, product, defect, evidenceFile, evidence, now, cancellationToken)
                : await UpdateSubmittedListingAsync(existing, submission, details, product, defect, evidenceFile, evidence, now, cancellationToken);
        }
        catch (DomainException ex)
        {
            return await AbortAsync(Result.Validation(ex.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return await AbortAsync(Result.Conflict(
                "This listing changed while you were editing it. Reload it and try again."));
        }
    }

    private async Task<Result<SaveListingOutcome>> CreateSubmittedListingAsync(
        Guid merchantId, ListingFormSubmission submission, ListingDetailsInput details,
        IReadOnlyList<StoredUpload> product, IReadOnlyList<StoredUpload> defect,
        StoredUpload? evidenceFile, IncomingEvidence? evidence, DateTime now, CancellationToken cancellationToken)
    {
        var baseSlug = Slug.Truncate(Slug.Create(submission.Title, "listing"), Listing.MaxSlugLength - 8);

        for (var attempt = 1; attempt <= MaxSlugAttempts; attempt++)
        {
            var slug = await NextAvailableSlugAsync(baseSlug, cancellationToken);

            var listing = new Listing(
                merchantId, submission.CategoryId, details.ConditionGradeId,
                submission.Title, slug, submission.Description, now);
            ApplyDetails(listing, details, now);
            listing.AddVariant(GenerateSku(), [], Math.Max(0, submission.Quantity), now);
            AddPhotos(listing, product, defect, now);
            AddEvidence(listing, evidenceFile, evidence, now);

            var (blockers, published, gateMessage) = await FinalizeSubmissionAsync(listing, now, cancellationToken);
            db.Listings.Add(listing);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Merchant {MerchantId} saved listing {ListingId} (published: {Published})",
                    merchantId, listing.Id, published);
                return Result<SaveListingOutcome>.Success(
                    new SaveListingOutcome(listing.Id, listing.Status, published, blockers, gateMessage));
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, ListingSlugIndex))
            {
                // Slug availability is check-then-insert; detach the failed graph and retry
                // against committed data within a small fixed budget.
                db.Listings.Remove(listing);
                if (attempt == MaxSlugAttempts)
                {
                    return Result<SaveListingOutcome>.Conflict(
                        "Another listing claimed this address while you were saving. Please try again.");
                }
            }
        }

        throw new InvalidOperationException("The listing slug retry loop exited unexpectedly.");
    }

    private async Task<Result<SaveListingOutcome>> UpdateSubmittedListingAsync(
        Listing listing, ListingFormSubmission submission, ListingDetailsInput details,
        IReadOnlyList<StoredUpload> product, IReadOnlyList<StoredUpload> defect,
        StoredUpload? evidenceFile, IncomingEvidence? evidence, DateTime now, CancellationToken cancellationToken)
    {
        ApplyDetails(listing, details, now);

        var variants = listing.Variants.ToList();
        var quantity = Math.Max(0, submission.Quantity);
        if (variants.Count == 0)
        {
            listing.AddVariant(GenerateSku(), [], quantity, now);
        }
        else if (variants.Count == 1)
        {
            listing.SetVariantStock(variants[0].Id, quantity, now);
        }

        var removedKeys = new List<string>();
        if (submission.RemovedPhotoIds.Count > 0)
        {
            var (gradeCode, reasonCodes) = await listing.LoadDisclosureCodesAsync(db, cancellationToken);
            foreach (var photoId in submission.RemovedPhotoIds)
            {
                removedKeys.Add(listing.RemoveMedia(photoId, gradeCode, reasonCodes, now));
            }
        }

        AddPhotos(listing, product, defect, now);
        AddEvidence(listing, evidenceFile, evidence, now);

        var (blockers, published, gateMessage) = await FinalizeSubmissionAsync(listing, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        foreach (var key in removedKeys)
        {
            await TryDeleteAsync(key, CancellationToken.None);
        }

        logger.LogInformation("Merchant saved listing {ListingId} (published: {Published})", listing.Id, published);
        return Result<SaveListingOutcome>.Success(
            new SaveListingOutcome(listing.Id, listing.Status, published, blockers, gateMessage));
    }

    /// <summary>
    /// Resolves the condition card to its stored grade id and discount-reason ids
    /// (<see cref="ConditionPresets"/>), then unions in any reasons the merchant added through
    /// the optional "Add another reason" link.
    /// </summary>
    private async Task<Result<(Guid ConditionGradeId, List<Guid> ReasonIds)>> ResolveConditionAsync(
        ListingFormSubmission submission, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(submission.Condition))
        {
            return Result<(Guid, List<Guid>)>.Validation("Choose the item's condition.");
        }

        var preset = ConditionPresets.For(submission.Condition);

        var gradeId = await db.ConditionGrades.AsNoTracking()
            .Where(g => g.Code == preset.GradeCode && g.IsActive)
            .Select(g => (Guid?)g.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (gradeId is null)
        {
            return Result<(Guid, List<Guid>)>.Validation("That condition is not available right now.");
        }

        var presetReasonIds = await db.DiscountReasons.AsNoTracking()
            .Where(r => preset.ReasonCodes.Contains(r.Code) && r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (presetReasonIds.Count != preset.ReasonCodes.Count)
        {
            return Result<(Guid, List<Guid>)>.Validation("That condition is not available right now.");
        }

        var reasonIds = presetReasonIds
            .Concat(submission.AdditionalDiscountReasonIds)
            .Distinct()
            .ToList();

        return Result<(Guid, List<Guid>)>.Success((gradeId.Value, reasonIds));
    }

    private static void AddPhotos(
        Listing listing, IReadOnlyList<StoredUpload> product, IReadOnlyList<StoredUpload> defect, DateTime now)
    {
        foreach (var upload in product)
        {
            listing.AddMedia(ListingMediaType.Product, upload.ObjectKey, upload.FileName,
                upload.ContentType, upload.SizeBytes, null, now);
        }

        foreach (var upload in defect)
        {
            listing.AddMedia(ListingMediaType.Defect, upload.ObjectKey, upload.FileName,
                upload.ContentType, upload.SizeBytes, null, now);
        }
    }

    private static void AddEvidence(
        Listing listing, StoredUpload? evidenceFile, IncomingEvidence? evidence, DateTime now)
    {
        if (evidence is null || (evidenceFile is null && string.IsNullOrWhiteSpace(evidence.ReferenceUrl)))
        {
            return;
        }

        listing.AddReferencePriceEvidence(
            evidence.EvidenceType,
            evidence.ReferenceUrl,
            evidenceFile?.ObjectKey,
            evidenceFile?.FileName,
            evidenceFile?.ContentType,
            note: null,
            now);
    }

    /// <summary>
    /// The publish decision for one save: report per-field blockers (leaving the listing a
    /// draft), or run the publish gate and submit for review. A material edit to an
    /// already-published listing has already moved it to PendingReview inside the aggregate,
    /// so the gate is only enforced on the true Draft/Rejected -> review transition.
    /// </summary>
    private async Task<(IReadOnlyList<SubmissionBlocker> Blockers, bool Published, string? GateMessage)>
        FinalizeSubmissionAsync(Listing listing, DateTime now, CancellationToken cancellationToken)
    {
        var (gradeCode, reasonCodes) = await listing.LoadDisclosureCodesAsync(db, cancellationToken);

        var blockers = listing.DescribeSubmissionBlockers(gradeCode, reasonCodes);
        if (blockers.Count > 0)
        {
            return (blockers, false, null);
        }

        if (listing.Status is ListingStatus.Draft or ListingStatus.Rejected)
        {
            var gate = await subscriptions.CheckPublishGateAsync(listing.MerchantProfileId, cancellationToken);
            if (!gate.CanPublish)
            {
                return (Array.Empty<SubmissionBlocker>(), false, gate.Message);
            }

            listing.SubmitForReview(gradeCode, reasonCodes, now);
        }

        var published = listing.Status
            is ListingStatus.PendingReview or ListingStatus.Live or ListingStatus.SoldOut;
        return (Array.Empty<SubmissionBlocker>(), published, null);
    }

    /// <summary>
    /// A unique stock-keeping code for the single auto-created variant. The merchant never
    /// sees it — the one-page form asks only for a quantity — so it carries no meaning.
    /// </summary>
    private static string GenerateSku() => $"SKU-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    public Task<Result> UpdateDetailsAsync(
        string userId, Guid listingId, ListingDetailsInput input, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, async (listing, now) =>
        {
            var validation = await ValidateDetailsAsync(input, cancellationToken);
            if (validation.Failed)
            {
                return validation;
            }

            ApplyDetails(listing, input, now);
            return Result.Success();
        }, cancellationToken);

    public Task<Result> AddOptionAsync(
        string userId, Guid listingId, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            if (listing.Options.Count >= _options.MaxOptionsPerListing)
            {
                return Task.FromResult(Result.Validation(
                    $"A listing can vary along at most {_options.MaxOptionsPerListing} options."));
            }

            listing.AddOption(name, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> RemoveOptionAsync(
        string userId, Guid listingId, Guid optionId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.RemoveOption(optionId, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> AddOptionValueAsync(
        string userId, Guid listingId, Guid optionId, string value, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            var option = listing.Options.SingleOrDefault(o => o.Id == optionId);
            if (option is not null && option.Values.Count >= _options.MaxValuesPerOption)
            {
                return Task.FromResult(Result.Validation(
                    $"An option can offer at most {_options.MaxValuesPerOption} values."));
            }

            listing.AddOptionValue(optionId, value, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> RemoveOptionValueAsync(
        string userId, Guid listingId, Guid optionId, Guid optionValueId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.RemoveOptionValue(optionId, optionValueId, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> AddVariantAsync(
        string userId, Guid listingId, AddVariantInput input, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            if (listing.Variants.Count >= _options.MaxVariantsPerListing)
            {
                return Task.FromResult(Result.Validation(
                    $"A listing can hold at most {_options.MaxVariantsPerListing} variants."));
            }

            listing.AddVariant(input.Sku, input.OptionValueIds, input.InitialQuantity, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> RemoveVariantAsync(
        string userId, Guid listingId, Guid variantId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.RemoveVariant(variantId, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> SetVariantActiveAsync(
        string userId, Guid listingId, Guid variantId, bool isActive, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.SetVariantActive(variantId, isActive, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public async Task<Result> AddImageAsync(
        string userId, Guid listingId, AddListingImageInput input, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(input.MediaType))
        {
            return Result.Validation("Choose a valid image kind.");
        }

        var metadata = ListingImageValidator.ValidateMetadata(
            input.OriginalFileName, input.ContentType, input.LengthBytes,
            _options.MaxImageBytes, ListingImageValidator.ImageContentTypes);
        if (metadata.Failed)
        {
            return metadata;
        }

        var stored = await BufferValidateAndStoreAsync(
            MediaContainer, input.Content, input.OriginalFileName, input.ContentType,
            ListingImageValidator.ImageContentTypes, cancellationToken);
        if (stored.Failed)
        {
            return stored;
        }

        var file = stored.Value;
        var result = await MutateAsync(userId, listingId, (listing, now) =>
        {
            if (listing.Media.Count(m => m.MediaType == input.MediaType) >= _options.MaxImagesPerType)
            {
                return Task.FromResult(Result.Validation(
                    $"A listing can hold at most {_options.MaxImagesPerType} images of one kind."));
            }

            listing.AddMedia(
                input.MediaType, file.ObjectKey, file.FileName, file.ContentType,
                file.SizeBytes, input.AltText, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

        if (result.Failed)
        {
            // The bytes are already stored but no row references them; do not leave an orphan.
            await TryDeleteAsync(file.ObjectKey, cancellationToken);
        }

        return result;
    }

    public async Task<Result> RemoveImageAsync(
        string userId, Guid listingId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        string? removedKey = null;
        var result = await MutateAsync(userId, listingId, async (listing, now) =>
        {
            var (conditionGradeCode, discountReasonCodes) = await listing.LoadDisclosureCodesAsync(db, cancellationToken);
            removedKey = listing.RemoveMedia(mediaId, conditionGradeCode, discountReasonCodes, now);
            return Result.Success();
        }, cancellationToken);

        if (result.Succeeded && removedKey is not null)
        {
            await TryDeleteAsync(removedKey, cancellationToken);
        }

        return result;
    }

    public async Task<Result> AddReferencePriceEvidenceAsync(
        string userId, Guid listingId, AddReferencePriceEvidenceInput input, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(input.EvidenceType))
        {
            return Result.Validation("Choose a valid evidence type.");
        }

        StoredUpload? file = null;
        if (input.Content is not null && input.LengthBytes > 0)
        {
            var metadata = ListingImageValidator.ValidateMetadata(
                input.OriginalFileName, input.ContentType, input.LengthBytes,
                _options.MaxImageBytes, ListingImageValidator.EvidenceContentTypes);
            if (metadata.Failed)
            {
                return metadata;
            }

            var stored = await BufferValidateAndStoreAsync(
                EvidenceContainer, input.Content, input.OriginalFileName!, input.ContentType!,
                ListingImageValidator.EvidenceContentTypes, cancellationToken);
            if (stored.Failed)
            {
                return stored;
            }

            file = stored.Value;
        }

        var result = await MutateAsync(userId, listingId, (listing, now) =>
        {
            if (listing.ReferencePriceEvidence.Count >= _options.MaxReferencePriceEvidencePerListing)
            {
                return Task.FromResult(Result.Validation(
                    $"A listing can hold at most {_options.MaxReferencePriceEvidencePerListing} evidence records."));
            }

            listing.AddReferencePriceEvidence(
                input.EvidenceType, input.ReferenceUrl, file?.ObjectKey, file?.FileName,
                file?.ContentType, input.Note, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

        if (result.Failed && file is not null)
        {
            await TryDeleteAsync(file.ObjectKey, cancellationToken);
        }

        return result;
    }

    public async Task<Result> RemoveReferencePriceEvidenceAsync(
        string userId, Guid listingId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        string? removedKey = null;
        var result = await MutateAsync(userId, listingId, (listing, now) =>
        {
            removedKey = listing.RemoveReferencePriceEvidence(evidenceId, now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

        if (result.Succeeded && removedKey is not null)
        {
            await TryDeleteAsync(removedKey, cancellationToken);
        }

        return result;
    }

    public Task<Result> SubmitForReviewAsync(
        string userId, Guid listingId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, async (listing, now) =>
        {
            // The publish gate: verified, subscribed and under quota, checked and reported
            // independently (CLAUDE.md invariant 7). Submitting is the merchant's "publish"
            // action — admin approval re-checks the same gate before the listing actually
            // goes live.
            var gate = await subscriptions.CheckPublishGateAsync(listing.MerchantProfileId, cancellationToken);
            if (!gate.CanPublish)
            {
                return Result.Forbidden(gate.Message!);
            }

            var (conditionGradeCode, discountReasonCodes) = await listing.LoadDisclosureCodesAsync(db, cancellationToken);

            var blockers = listing.DescribeSubmissionBlockers(conditionGradeCode, discountReasonCodes);
            if (blockers.Count > 0)
            {
                return Result.Validation(blockers[0].Message);
            }

            listing.SubmitForReview(conditionGradeCode, discountReasonCodes, now);
            logger.LogInformation("Listing {ListingId} submitted for moderation", listing.Id);
            return Result.Success();
        }, cancellationToken);

    public Task<Result> HideAsync(string userId, Guid listingId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.Hide(now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result> RestoreAsync(string userId, Guid listingId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, async (listing, now) =>
        {
            // Restoring a paused listing consumes a quota slot again, so it passes through the
            // same publish gate as submission.
            var gate = await subscriptions.CheckPublishGateAsync(listing.MerchantProfileId, cancellationToken);
            if (!gate.CanPublish)
            {
                return Result.Forbidden(gate.Message!);
            }

            listing.Restore(now);
            return Result.Success();
        }, cancellationToken);

    public Task<Result> ArchiveAsync(string userId, Guid listingId, CancellationToken cancellationToken = default) =>
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.Archive(now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    // ---- Internals ------------------------------------------------------------------

    /// <summary>
    /// Loads the caller's own listing, runs one aggregate mutation and saves. Ownership is
    /// re-resolved from the database on every call, so a guessed listing id reads as
    /// "not found" rather than exposing another merchant's stock
    /// </summary>
    private async Task<Result> MutateAsync(
        string userId,
        Guid listingId,
        Func<Listing, DateTime, Task<Result>> mutate,
        CancellationToken cancellationToken)
    {
        var merchant = await RequireRegisteredMerchantAsync(userId, cancellationToken);
        if (merchant.Failed)
        {
            return merchant;
        }

        var listing = await db.Listings
            .WithAggregate()
            .SingleOrDefaultAsync(l => l.Id == listingId && l.MerchantProfileId == merchant.Value, cancellationToken);

        if (listing is null)
        {
            return Result.NotFound("That listing was not found.");
        }

        Result outcome;
        try
        {
            outcome = await mutate(listing, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Validation(ex.Message);
        }

        if (outcome.Failed)
        {
            return outcome;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // An admin decided this listing, or the merchant edited it in another tab, between
            // the read and the write.
            return Result.Conflict("This listing changed while you were editing it. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex, "IX_ListingVariants_ListingId_OptionCombinationKey"))
        {
            // The aggregate already refuses duplicates; this is the database backstop for two
            // concurrent requests adding the same combination.
            return Result.Conflict("A variant with this combination already exists on this listing.");
        }

        return Result.Success();
    }

    private void ApplyDetails(Listing listing, ListingDetailsInput input, DateTime now)
    {
        listing.UpdateDetails(
            input.CategoryId,
            input.ConditionGradeId,
            input.Title,
            input.Description,
            input.ReferencePrice,
            input.RetailPrice,
            input.ReturnPolicyText,
            input.WarrantyType,
            input.WarrantyMonths,
            input.IncludedItemsText,
            input.MissingItemsText,
            input.DiscountReasonIds,
            now);
    }

    /// <summary>
    /// Checks everything the aggregate cannot see for itself: that the referenced catalog rows
    /// exist and are active.
    /// </summary>
    private async Task<Result> ValidateDetailsAsync(ListingDetailsInput input, CancellationToken cancellationToken)
    {
        // A listing attaches to a leaf category, never a sector root: the root
        // itself is not a shoppable category, and the reference
        // data offered to the form already excludes it — this rejects a crafted request that
        // posts the root id directly.
        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: true, includeRoot: false, cancellationToken);
        if (!launchCategoryIds.Contains(input.CategoryId))
        {
            return Result.Validation("Choose a category.");
        }

        if (!await db.ConditionGrades.AnyAsync(g => g.Id == input.ConditionGradeId && g.IsActive, cancellationToken))
        {
            return Result.Validation("Choose a condition grade.");
        }

        var reasonIds = input.DiscountReasonIds.Distinct().ToList();
        if (reasonIds.Count > 0)
        {
            var known = await db.DiscountReasons
                .CountAsync(r => reasonIds.Contains(r.Id) && r.IsActive, cancellationToken);
            if (known != reasonIds.Count)
            {
                return Result.Validation("One of the selected discount reasons is no longer available.");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// The listing-workspace gate: a merchant profile that has not been suspended. Draft
    /// creation and editing need only this — verification approval and an active subscription
    /// are checked separately, and only at the point a listing actually tries to become public
    /// (<see cref="SubmitForReviewAsync"/>, <see cref="RestoreAsync"/>).
    /// </summary>
    private async Task<Result<Guid>> RequireRegisteredMerchantAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.VerificationStatus })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return Result<Guid>.Forbidden("Complete your merchant application before creating listings.");
        }

        // Defence in depth: the MVC route already requires the RegisteredMerchant policy, but
        // a suspension between the two checks must still stop the write.
        return profile.VerificationStatus == MerchantVerificationStatus.Suspended
            ? Result<Guid>.Forbidden("Your merchant account is suspended and cannot manage listings.")
            : Result<Guid>.Success(profile.Id);
    }

    private Task<Guid?> ResolveMerchantIdAsync(string userId, CancellationToken cancellationToken) =>
        db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<string> NextAvailableSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (await db.Listings.AsNoTracking().AnyAsync(l => l.Slug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    /// <summary>
    /// Buffers the upload once so its bytes can be inspected before anything is stored, and so
    /// the recorded length is the real one rather than a client-reported figure
    /// </summary>
    private async Task<Result<StoredUpload>> BufferValidateAndStoreAsync(
        string container,
        Stream content,
        string originalFileName,
        string contentType,
        IReadOnlyDictionary<string, string[]> accepted,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return Result<StoredUpload>.Validation("The file is empty.");
        }

        var metadata = ListingImageValidator.ValidateMetadata(
            originalFileName, contentType, buffer.Length, _options.MaxImageBytes, accepted);
        if (metadata.Failed)
        {
            return Result<StoredUpload>.From(metadata);
        }

        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        var payload = ListingImageValidator.ValidatePayload(
            buffer.GetBuffer().AsSpan(0, (int)buffer.Length), normalizedContentType);
        if (payload.Failed)
        {
            return Result<StoredUpload>.From(payload);
        }

        buffer.Position = 0;
        var objectKey = await fileStorage.SaveAsync(container, buffer, originalFileName, cancellationToken);
        return Result<StoredUpload>.Success(
            new StoredUpload(objectKey, SafeFileName(originalFileName), normalizedContentType, buffer.Length));
    }

    private async Task TryDeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(objectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up orphaned listing file {ObjectKey}", objectKey);
        }
    }

    private static string SafeFileName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName ?? string.Empty).Trim();
        return string.IsNullOrEmpty(name) ? "image" : name;
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

    private static bool IsForeignKeyViolation(DbUpdateException exception, string constraintName)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && sqlException.Number == 547
                && sqlException.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record StoredUpload(string ObjectKey, string FileName, string ContentType, long SizeBytes);
}
