using Faed.Web.Models;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Abstractions;
using Faed.Web.Services.Catalog;
using Faed.Web.Services.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faed.Web.Services.Listings;

/// <inheritdoc />
public sealed class MerchantListingService(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    IClock clock,
    IOptions<ListingOptions> options,
    ILogger<MerchantListingService> logger) : IMerchantListingService
{
    private const string MediaContainer = "listing-media";
    private const string EvidenceContainer = "listing-evidence";
    private const string ListingSlugIndex = "IX_Listings_Slug";
    private const string B2BOfferLineVariantForeignKey = "FK_B2BOfferLines_ListingVariants_ListingVariantId";
    private const int MaxSlugAttempts = 5;

    private readonly ListingOptions _options = options.Value;

    public async Task<ListingReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default)
    {
        // Categories, grades and reasons are admin-managed reference data; the form must read
        // them from the database rather than hard-code the launch taxonomy (TASK-003).
        var launchCategoryIds = await LaunchCatalogScope.GetCategoryIdsAsync(
            db, activeOnly: true, includeRoot: false, cancellationToken);
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => launchCategoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CatalogChoice(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        var grades = await db.ConditionGrades
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .Select(g => new CatalogChoice(g.Id, $"Grade {g.Code} — {g.Name}"))
            .ToListAsync(cancellationToken);

        var reasons = await db.DiscountReasons
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new CatalogChoice(r.Id, r.Name))
            .ToListAsync(cancellationToken);

        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new CatalogChoice(b.Id, b.Name))
            .ToListAsync(cancellationToken);

        return new ListingReferenceData(categories, grades, reasons, brands);
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
            // and not archived is waiting on them (.claude/skills/faed-dashboard-ux).
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
        var merchant = await RequireApprovedMerchantAsync(userId, cancellationToken);
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
        MutateAsync(userId, listingId, async (listing, now) =>
        {
            // The adjustment audit outlives the variant it describes, so a variant whose stock
            // has already been corrected is deactivated rather than deleted
            // (docs/03-BUSINESS-RULES.md §6, docs/04-DOMAIN-MODEL.md §12).
            if (await db.InventoryAdjustments.AnyAsync(a => a.ListingVariantId == variantId, cancellationToken))
            {
                return Result.Validation(
                    "This variant has a stock adjustment history. Deactivate it instead of removing it.");
            }

            if (listing.Variants.Any(v => v.Id == variantId)
                && await db.B2BOfferLines.AnyAsync(l => l.ListingVariantId == variantId, cancellationToken))
            {
                return Result.Validation(
                    "This variant is part of wholesale offer history. Deactivate it instead of removing it.");
            }

            listing.RemoveVariant(variantId, now);
            return Result.Success();
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
            var (conditionGradeCode, discountReasonCodes) = await listing.LoadDisclosureCodesAsync(db, cancellationToken);

            var blockers = listing.DescribeSubmissionBlockers(conditionGradeCode, discountReasonCodes);
            if (blockers.Count > 0)
            {
                return Result.Validation(blockers[0]);
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
        MutateAsync(userId, listingId, (listing, now) =>
        {
            listing.Restore(now);
            return Task.FromResult(Result.Success());
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
    /// (docs/08-SECURITY-AND-PRIVACY.md §9).
    /// </summary>
    private async Task<Result> MutateAsync(
        string userId,
        Guid listingId,
        Func<Listing, DateTime, Task<Result>> mutate,
        CancellationToken cancellationToken)
    {
        var merchant = await RequireApprovedMerchantAsync(userId, cancellationToken);
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
            // concurrent requests adding the same combination (docs/17-DATA-INVARIANTS.md).
            return Result.Conflict("A variant with this combination already exists on this listing.");
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex, B2BOfferLineVariantForeignKey))
        {
            return Result.Validation(
                "This variant is part of wholesale offer history. Deactivate it instead of removing it.");
        }

        return Result.Success();
    }

    private void ApplyDetails(Listing listing, ListingDetailsInput input, DateTime now)
    {
        var minimumQuantity = input.AllowB2B
            ? input.WholesaleMinQuantity ?? _options.DefaultB2BMinimumQuantity
            : input.WholesaleMinQuantity;

        listing.UpdateDetails(
            input.CategoryId,
            input.BrandId,
            input.ConditionGradeId,
            input.Title,
            input.Description,
            input.ReferencePrice,
            input.RetailPrice,
            input.WholesaleIndicativeUnitPrice,
            minimumQuantity,
            input.AllowB2C,
            input.AllowB2B,
            input.AllowMixedVariantB2B,
            input.ReturnPolicyText,
            input.WarrantyText,
            input.IncludedItemsText,
            input.MissingItemsText,
            input.DiscountReasonIds,
            now);
    }

    /// <summary>
    /// Checks everything the aggregate cannot see for itself: that the referenced catalog rows
    /// exist and are active, and that the B2B minimum respects the configured launch floor
    /// (docs/03-BUSINESS-RULES.md §11 — a policy default, not a domain constant).
    /// </summary>
    private async Task<Result> ValidateDetailsAsync(ListingDetailsInput input, CancellationToken cancellationToken)
    {
        // A listing attaches to a leaf category, never a sector root: "Fashion Overstock"
        // itself is not a shoppable category (docs/04-DOMAIN-MODEL.md §2), and the reference
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

        if (input.BrandId is { } brandId
            && !await db.Brands.AnyAsync(b => b.Id == brandId && b.IsActive, cancellationToken))
        {
            return Result.Validation("Choose a brand from the list, or leave it blank.");
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

        if (input.AllowB2B
            && input.WholesaleMinQuantity is { } requested
            && requested < _options.DefaultB2BMinimumQuantity)
        {
            return Result.Validation(
                $"The B2B minimum order quantity cannot be below {_options.DefaultB2BMinimumQuantity} units.");
        }

        return Result.Success();
    }

    private async Task<Result<Guid>> RequireApprovedMerchantAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await db.MerchantProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.VerificationStatus })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return Result<Guid>.Forbidden("Complete merchant verification before creating listings.");
        }

        // Defence in depth: the MVC route already requires the ApprovedMerchant policy, but a
        // suspension between the two checks must still stop the write (AGENTS.md §3).
        return profile.VerificationStatus == MerchantVerificationStatus.Approved
            ? Result<Guid>.Success(profile.Id)
            : Result<Guid>.Forbidden(
                $"Your merchant account is {profile.VerificationStatus} and cannot manage listings.");
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
    /// (docs/08-SECURITY-AND-PRIVACY.md §4).
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
