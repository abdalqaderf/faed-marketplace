using Faed.Web.Models;
using Faed.Web.Models.Enums;

namespace Faed.Web.Models.Entities;

/// <summary>
/// A merchant's offer of surplus or non-perfect stock: the aggregate root that owns its
/// options, sellable variants, media, discount reasons, reference-price evidence and
/// moderation history (docs/04-DOMAIN-MODEL.md §3-5).
///
/// Two rules shape this type:
/// <list type="bullet">
/// <item>It holds no authoritative stock quantity. Inventory lives on
/// <see cref="ListingVariant"/> (AGENTS.md Rule A, docs/adr/0002).</item>
/// <item>A merchant cannot change what a published listing materially claims without a new
/// review. Every material mutator routes through <c>ApplyMaterialChange</c>, which takes a
/// Live listing out of public view and opens a fresh <see cref="ListingModeration"/> row
/// (AGENTS.md §8, docs/02-SCOPE-AND-DECISIONS.md "Listing moderation policy").</item>
/// </list>
/// </summary>
public class Listing
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 200;
    public const int MaxSlugLength = 240;
    public const int MaxDescriptionLength = 4000;
    public const int MaxPolicyTextLength = 2000;
    public const int MaxDecisionReasonLength = 1000;

    private readonly List<ListingOption> _options = [];
    private readonly List<ListingVariant> _variants = [];
    private readonly List<ListingMedia> _media = [];
    private readonly List<ListingDiscountReason> _discountReasons = [];
    private readonly List<ListingReferencePriceEvidence> _referencePriceEvidence = [];
    private readonly List<ListingModeration> _moderations = [];

    private Listing()
    {
    }

    public Listing(
        Guid merchantProfileId,
        Guid categoryId,
        Guid conditionGradeId,
        string title,
        string slug,
        string description,
        DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        MerchantProfileId = merchantProfileId;
        CategoryId = categoryId;
        ConditionGradeId = conditionGradeId;
        Title = RequireText(title, "title", MinTitleLength, MaxTitleLength);
        Slug = RequireText(slug, "slug", 1, MaxSlugLength);
        Description = RequireText(description, "description", 1, MaxDescriptionLength);
        Status = ListingStatus.Draft;
        AllowB2C = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>The selling merchant. A listing belongs to exactly one (docs/17-DATA-INVARIANTS.md).</summary>
    public Guid MerchantProfileId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public Guid ConditionGradeId { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>Public routing identifier. Never an authorization key (docs/06-ARCHITECTURE.md §12).</summary>
    public string Slug { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>What the item normally sells for. Requires provenance evidence to be submitted.</summary>
    public decimal? ReferencePrice { get; private set; }

    /// <summary>The B2C unit price. Required when <see cref="AllowB2C"/> is set.</summary>
    public decimal? RetailPrice { get; private set; }

    /// <summary>Indicative wholesale unit price; the binding B2B price comes from a negotiation.</summary>
    public decimal? WholesaleIndicativeUnitPrice { get; private set; }

    /// <summary>Minimum B2B order quantity. Required and positive when <see cref="AllowB2B"/> is set.</summary>
    public int? WholesaleMinQuantity { get; private set; }

    public bool AllowB2C { get; private set; }

    public bool AllowB2B { get; private set; }

    /// <summary>Whether mixed variants may be combined toward the B2B minimum (docs/03-BUSINESS-RULES.md §11).</summary>
    public bool AllowMixedVariantB2B { get; private set; }

    public string? ReturnPolicyText { get; private set; }

    public string? WarrantyText { get; private set; }

    public string? IncludedItemsText { get; private set; }

    public string? MissingItemsText { get; private set; }

    public ListingStatus Status { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    /// <summary>
    /// True while this listing is Hidden specifically because an admin took it down for a
    /// policy reason, as opposed to the merchant pausing it themselves. Only
    /// <see cref="RestoreByAdmin"/> can clear it (docs/16-PERMISSIONS-MATRIX.md
    /// "Moderate listing — Admin only").
    /// </summary>
    public bool HiddenByAdmin { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Guards a merchant edit racing an admin moderation decision on the same listing.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ListingOption> Options => _options.AsReadOnly();

    public IReadOnlyCollection<ListingVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyCollection<ListingMedia> Media => _media.AsReadOnly();

    public IReadOnlyCollection<ListingDiscountReason> DiscountReasons => _discountReasons.AsReadOnly();

    public IReadOnlyCollection<ListingReferencePriceEvidence> ReferencePriceEvidence =>
        _referencePriceEvidence.AsReadOnly();

    public IReadOnlyCollection<ListingModeration> Moderations => _moderations.AsReadOnly();

    /// <summary>Only a Live listing is public (docs/03-BUSINESS-RULES.md §2).</summary>
    public bool IsPubliclyVisible => Status == ListingStatus.Live;

    /// <summary>Sum of available stock across sellable variants. Derived, never stored.</summary>
    public int AvailableUnits => _variants.Where(v => v.IsActive).Sum(v => v.AvailableQuantity);

    /// <summary>A material edit is accepted in these states; Archived and PendingReview are frozen.</summary>
    public bool AcceptsMaterialEdit => Status is ListingStatus.Draft or ListingStatus.Rejected
        or ListingStatus.Live or ListingStatus.Hidden or ListingStatus.SoldOut;

    public bool IsArchived => Status == ListingStatus.Archived;

    public ListingModeration? PendingModeration => _moderations.SingleOrDefault(m => m.IsPending);

    public ListingModeration? LatestModeration =>
        _moderations.OrderByDescending(m => m.SubmittedAtUtc).FirstOrDefault();

    // ---- Merchant edits -------------------------------------------------------------

    /// <summary>
    /// Applies the merchant's business details in one operation. Fields the moderation
    /// policy calls material (docs/02-SCOPE-AND-DECISIONS.md) are compared against the
    /// stored values, and any difference sends a published listing back for review.
    /// </summary>
    public void UpdateDetails(
        Guid categoryId,
        Guid? brandId,
        Guid conditionGradeId,
        string title,
        string description,
        decimal? referencePrice,
        decimal? retailPrice,
        decimal? wholesaleIndicativeUnitPrice,
        int? wholesaleMinQuantity,
        bool allowB2C,
        bool allowB2B,
        bool allowMixedVariantB2B,
        string? returnPolicyText,
        string? warrantyText,
        string? includedItemsText,
        string? missingItemsText,
        IReadOnlyCollection<Guid> discountReasonIds,
        DateTime nowUtc)
    {
        // Checked once, up front, for every field this call touches — including discount
        // reasons. Applying them through separate aggregate calls would let the first call's
        // Live -> PendingReview transition (see ApplyMaterialChange) make the *second* call
        // see a listing that no longer accepts edits, failing an edit that changed nothing
        // material about the reasons at all.
        RequireMaterialEditAllowed();

        title = RequireText(title, "title", MinTitleLength, MaxTitleLength);
        description = RequireText(description, "description", 1, MaxDescriptionLength);
        returnPolicyText = OptionalText(returnPolicyText, "return policy", MaxPolicyTextLength);
        warrantyText = OptionalText(warrantyText, "warranty text", MaxPolicyTextLength);
        includedItemsText = OptionalText(includedItemsText, "included items", MaxPolicyTextLength);
        missingItemsText = OptionalText(missingItemsText, "missing items", MaxPolicyTextLength);
        RequireNonNegative(referencePrice, "Reference price");
        RequireNonNegative(retailPrice, "Retail price");
        RequireNonNegative(wholesaleIndicativeUnitPrice, "Wholesale price");

        if (wholesaleMinQuantity is <= 0)
        {
            throw new DomainException("The B2B minimum order quantity must be greater than zero.");
        }

        var changes = new List<string>();
        Compare(changes, "category", CategoryId != categoryId);
        Compare(changes, "brand", BrandId != brandId);
        Compare(changes, "condition grade", ConditionGradeId != conditionGradeId);
        Compare(changes, "title", !string.Equals(Title, title, StringComparison.Ordinal));
        Compare(changes, "description", !string.Equals(Description, description, StringComparison.Ordinal));
        Compare(changes, "included items", !string.Equals(IncludedItemsText, includedItemsText, StringComparison.Ordinal));
        Compare(changes, "missing items", !string.Equals(MissingItemsText, missingItemsText, StringComparison.Ordinal));
        Compare(changes, "reference price", ReferencePrice != referencePrice);
        Compare(changes, "retail price", RetailPrice != retailPrice);
        Compare(changes, "wholesale price", WholesaleIndicativeUnitPrice != wholesaleIndicativeUnitPrice);
        Compare(changes, "B2B minimum quantity", WholesaleMinQuantity != wholesaleMinQuantity);
        Compare(changes, "sales channels", AllowB2C != allowB2C || AllowB2B != allowB2B);

        var requestedReasons = discountReasonIds.Distinct().ToHashSet();
        var currentReasons = _discountReasons.Select(r => r.DiscountReasonId).ToHashSet();
        Compare(changes, "discount reasons", !requestedReasons.SetEquals(currentReasons));

        CategoryId = categoryId;
        BrandId = brandId;
        ConditionGradeId = conditionGradeId;
        Title = title;
        Description = description;
        IncludedItemsText = includedItemsText;
        MissingItemsText = missingItemsText;
        ReferencePrice = referencePrice;
        RetailPrice = retailPrice;
        WholesaleIndicativeUnitPrice = wholesaleIndicativeUnitPrice;
        WholesaleMinQuantity = wholesaleMinQuantity;
        AllowB2C = allowB2C;
        AllowB2B = allowB2B;

        // Return policy, warranty and the mixed-lot flag are commercial terms rather than
        // claims about the product, so they are deliberately not on the material list.
        AllowMixedVariantB2B = allowMixedVariantB2B;
        ReturnPolicyText = returnPolicyText;
        WarrantyText = warrantyText;

        _discountReasons.RemoveAll(r => !requestedReasons.Contains(r.DiscountReasonId));
        foreach (var id in requestedReasons.Where(id => !currentReasons.Contains(id)))
        {
            _discountReasons.Add(new ListingDiscountReason(id));
        }

        if (changes.Count > 0)
        {
            ApplyMaterialChange(string.Join(", ", changes) + " changed", nowUtc);
        }
        else
        {
            Touch(nowUtc);
        }
    }

    public ListingOption AddOption(string name, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        name = RequireText(name, "option name", 1, ListingOption.MaxNameLength);

        if (_options.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"This listing already has an option called {name}.");
        }

        if (_variants.Count > 0)
        {
            // Adding a dimension after variants exist would leave every existing variant with
            // an incomplete combination, so the merchant must clear the variants first.
            throw new DomainException("Remove the existing variants before changing the option structure.");
        }

        var option = new ListingOption(name, _options.Count);
        _options.Add(option);
        ApplyMaterialChange($"option {name} added", nowUtc);
        return option;
    }

    public void RemoveOption(Guid optionId, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var option = FindOption(optionId);

        if (_variants.Count > 0)
        {
            throw new DomainException("Remove the existing variants before changing the option structure.");
        }

        _options.Remove(option);
        ApplyMaterialChange($"option {option.Name} removed", nowUtc);
    }

    public ListingOptionValue AddOptionValue(Guid optionId, string value, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var option = FindOption(optionId);
        value = RequireText(value, "option value", 1, ListingOptionValue.MaxValueLength);

        if (option.HasValue(value))
        {
            throw new DomainException($"{option.Name} already offers {value}.");
        }

        var added = option.AddValue(value, option.Values.Count);
        ApplyMaterialChange($"{option.Name} value {value} added", nowUtc);
        return added;
    }

    public void RemoveOptionValue(Guid optionId, Guid optionValueId, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var option = FindOption(optionId);

        if (_variants.Any(v => v.OptionValues.Any(ov => ov.ListingOptionValueId == optionValueId)))
        {
            throw new DomainException("Remove the variants that use this value first.");
        }

        if (!option.RemoveValue(optionValueId))
        {
            throw new DomainException("That option value is not part of this listing.");
        }

        ApplyMaterialChange($"{option.Name} value removed", nowUtc);
    }

    /// <summary>
    /// Adds a sellable variant. The combination must name exactly one value per listing
    /// option and must not duplicate an existing variant (docs/17-DATA-INVARIANTS.md).
    /// </summary>
    public ListingVariant AddVariant(
        string sku,
        IReadOnlyCollection<Guid> optionValueIds,
        int initialQuantity,
        DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        sku = RequireText(sku, "SKU", 1, ListingVariant.MaxSkuLength);

        if (_variants.Any(v => string.Equals(v.Sku, sku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"SKU {sku} is already used by another variant of this listing.");
        }

        ValidateCombination(optionValueIds);

        if (_variants.Any(v => v.MatchesCombination(optionValueIds)))
        {
            throw new DomainException("A variant with this combination already exists on this listing.");
        }

        var variant = new ListingVariant(sku, optionValueIds, initialQuantity, nowUtc);
        _variants.Add(variant);
        ApplyMaterialChange($"variant {sku} added", nowUtc);
        return variant;
    }

    public void RemoveVariant(Guid variantId, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var variant = FindVariant(variantId);

        if (variant.ReservedQuantity > 0 || variant.SoldQuantity > 0)
        {
            // Transactional history must survive; deactivate instead of deleting
            // (docs/04-DOMAIN-MODEL.md §12).
            throw new DomainException(
                "This variant has reserved or sold stock. Deactivate it instead of removing it.");
        }

        _variants.Remove(variant);
        ApplyMaterialChange($"variant {variant.Sku} removed", nowUtc);
    }

    public void SetVariantActive(Guid variantId, bool isActive, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var variant = FindVariant(variantId);
        if (variant.IsActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            variant.Reactivate(nowUtc);
        }
        else
        {
            variant.Deactivate(nowUtc);
        }

        ApplyMaterialChange($"variant {variant.Sku} {(isActive ? "reactivated" : "deactivated")}", nowUtc);
    }

    /// <summary>
    /// Image kinds that are part of what the listing publicly claims, so adding or removing
    /// one on a published listing is a material change that must be re-reviewed before it is
    /// visible: the primary <see cref="ListingMediaType.Product"/> gallery a buyer judges the
    /// item by, and <see cref="ListingMediaType.Defect"/> disclosure evidence
    /// (AGENTS.md §8 "Do not let a merchant edit a live listing … and bypass review",
    /// docs/03-BUSINESS-RULES.md §3). Ordinary packaging shots are not on this list.
    /// </summary>
    private static bool IsMaterialMedia(ListingMediaType mediaType) =>
        mediaType is ListingMediaType.Product or ListingMediaType.Defect;

    /// <summary>
    /// Adds an image. Product and defect photography are what a buyer judges the item by, so
    /// adding or removing one on a published listing routes through moderation
    /// (<see cref="IsMaterialMedia"/>); ordinary packaging shots are not material.
    /// </summary>
    public ListingMedia AddMedia(
        ListingMediaType mediaType,
        string storageObjectKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? altText,
        DateTime nowUtc)
    {
        RequireNotArchived();
        if (IsMaterialMedia(mediaType))
        {
            RequireMaterialEditAllowed();
        }

        altText = OptionalText(altText, "image description", ListingMedia.MaxAltTextLength);
        var sortOrder = _media.Count(m => m.MediaType == mediaType);
        var media = new ListingMedia(
            mediaType, storageObjectKey, originalFileName, contentType, sizeBytes, altText, sortOrder, nowUtc);
        _media.Add(media);

        if (IsMaterialMedia(mediaType))
        {
            ApplyMaterialChange(
                mediaType == ListingMediaType.Product ? "product photo added" : "defect photo added", nowUtc);
        }
        else
        {
            Touch(nowUtc);
        }

        return media;
    }

    /// <summary>
    /// Removes an image and returns its storage key so the caller can delete the bytes. The
    /// caller supplies the resolved catalog codes (as for <see cref="DescribeSubmissionBlockers"/>)
    /// so the aggregate can refuse to drop the last piece of disclosure evidence a published
    /// listing is required to carry.
    /// </summary>
    public string RemoveMedia(
        Guid mediaId,
        string conditionGradeCode,
        IReadOnlyCollection<string> discountReasonCodes,
        DateTime nowUtc)
    {
        RequireNotArchived();
        var media = _media.SingleOrDefault(m => m.Id == mediaId)
            ?? throw new DomainException("That image is not part of this listing.");

        if (IsMaterialMedia(media.MediaType))
        {
            RequireMaterialEditAllowed();
        }

        if (media.MediaType == ListingMediaType.Product
            && _media.Count(m => m.MediaType == ListingMediaType.Product) <= 1)
        {
            // Submission requires at least one product photo (DescribeSubmissionBlockers); a
            // Live listing dropping to zero would silently violate that invariant, since
            // removing an ordinary photo does not by itself re-run the submission checks.
            // Add a replacement photo before removing the last one.
            throw new DomainException(
                "A listing must keep at least one product photo. Add another before removing this one.");
        }

        if (media.MediaType is ListingMediaType.Defect or ListingMediaType.Packaging
            && _media.Count(m => m.MediaType is ListingMediaType.Defect or ListingMediaType.Packaging) <= 1
            && DisclosesAPhysicalImperfection(conditionGradeCode, discountReasonCodes))
        {
            // Same reasoning as the last-product-photo guard: this listing's condition grade or
            // discount reason discloses a physical imperfection, so it must keep at least one
            // defect or packaging photo showing it (docs/03-BUSINESS-RULES.md §3). Removing an
            // ordinary packaging photo is not otherwise material and does not re-run the
            // submission checks, so the aggregate refuses outright rather than let the listing
            // stay/return public with no visual evidence.
            throw new DomainException(
                "This listing discloses a physical imperfection, so it must keep at least one " +
                "defect or packaging photo. Add another before removing this one.");
        }

        _media.Remove(media);

        if (IsMaterialMedia(media.MediaType))
        {
            ApplyMaterialChange(
                media.MediaType == ListingMediaType.Product ? "product photo removed" : "defect photo removed", nowUtc);
        }
        else
        {
            Touch(nowUtc);
        }

        return media.StorageObjectKey;
    }

    public ListingReferencePriceEvidence AddReferencePriceEvidence(
        ReferencePriceEvidenceType evidenceType,
        string? referenceUrl,
        string? storageObjectKey,
        string? originalFileName,
        string? contentType,
        string? note,
        DateTime nowUtc)
    {
        RequireNotArchived();
        referenceUrl = OptionalText(referenceUrl, "reference URL", ListingReferencePriceEvidence.MaxReferenceUrlLength);
        referenceUrl = RequireHttpUrl(referenceUrl);
        note = OptionalText(note, "note", ListingReferencePriceEvidence.MaxNoteLength);

        if (referenceUrl is null && storageObjectKey is null && note is null)
        {
            throw new DomainException("Reference-price evidence needs a link, a file or a note.");
        }

        var evidence = new ListingReferencePriceEvidence(
            evidenceType, referenceUrl, storageObjectKey, originalFileName, contentType, note, nowUtc);
        _referencePriceEvidence.Add(evidence);

        // Adding provenance only strengthens the claim, so it never re-opens moderation.
        Touch(nowUtc);
        return evidence;
    }

    /// <summary>Removes evidence and returns its storage key, if it had an uploaded file.</summary>
    public string? RemoveReferencePriceEvidence(Guid evidenceId, DateTime nowUtc)
    {
        RequireMaterialEditAllowed();
        var evidence = _referencePriceEvidence.SingleOrDefault(e => e.Id == evidenceId)
            ?? throw new DomainException("That evidence is not part of this listing.");

        _referencePriceEvidence.Remove(evidence);

        // Withdrawing provenance weakens a published price claim, so it is material.
        ApplyMaterialChange("reference-price evidence removed", nowUtc);
        return evidence.StorageObjectKey;
    }

    // ---- Lifecycle ------------------------------------------------------------------

    /// <summary>
    /// Condition grades whose own description names a physical imperfection
    /// (docs/12-SEED-DATA.md: Grade B "packaging imperfection", Grade D "cosmetic
    /// imperfection") — a listing carrying one of these must show the imperfection, not
    /// merely claim it (docs/03-BUSINESS-RULES.md §3 "defects must be disclosed and
    /// visually evidenced where applicable").
    /// </summary>
    private static readonly HashSet<string> ConditionGradeCodesRequiringEvidence =
        new(StringComparer.OrdinalIgnoreCase) { "B", "D" };

    /// <summary>Discount reasons that are themselves a claim about a physical defect.</summary>
    private static readonly HashSet<string> DiscountReasonCodesRequiringEvidence =
        new(StringComparer.OrdinalIgnoreCase) { "PackagingDamage", "CosmeticDefect" };

    /// <summary>
    /// True when this listing's condition grade or one of its discount reasons is itself a
    /// claim about a physical imperfection, which must be shown and not merely stated
    /// (docs/03-BUSINESS-RULES.md §3). Both codes are resolved by the caller — the aggregate
    /// stores only the catalog ids (docs/06-ARCHITECTURE.md "Enums vs tables").
    /// </summary>
    public bool DisclosesAPhysicalImperfection(
        string conditionGradeCode, IReadOnlyCollection<string> discountReasonCodes) =>
        ConditionGradeCodesRequiringEvidence.Contains(conditionGradeCode)
        || discountReasonCodes.Any(DiscountReasonCodesRequiringEvidence.Contains);

    /// <summary>
    /// Everything that stops this listing being published, as merchant-facing sentences.
    /// Empty means the listing is submittable (docs/17-DATA-INVARIANTS.md "Listing").
    /// </summary>
    /// <param name="conditionGradeCode">The stable <see cref="ConditionGrade.Code"/> for
    /// <see cref="ConditionGradeId"/>.</param>
    /// <param name="discountReasonCodes">The stable <see cref="DiscountReason.Code"/>s for
    /// every id in <see cref="DiscountReasons"/>.</param>
    /// <remarks>
    /// Both parameters are resolved by the caller: the aggregate stores only the catalog
    /// ids, never a denormalized copy of admin-managed reference text
    /// (docs/06-ARCHITECTURE.md, "Enums vs tables").
    /// </remarks>
    public IReadOnlyList<string> DescribeSubmissionBlockers(
        string conditionGradeCode, IReadOnlyCollection<string> discountReasonCodes)
    {
        var problems = new List<string>();

        if (!AllowB2C && !AllowB2B)
        {
            problems.Add("Enable retail selling, wholesale selling, or both.");
        }

        if (AllowB2C && RetailPrice is null)
        {
            problems.Add("A retail price is required when the listing is sold to individual buyers.");
        }

        if (AllowB2B && WholesaleMinQuantity is null or <= 0)
        {
            problems.Add("A positive B2B minimum order quantity is required when wholesale is enabled.");
        }

        if (ReferencePrice is { } reference && RetailPrice is { } retail && reference <= retail)
        {
            problems.Add("The reference price must be higher than the Faed retail price.");
        }

        if (ReferencePrice is not null && _referencePriceEvidence.Count == 0)
        {
            problems.Add("Add evidence for the reference price, or remove the reference price.");
        }

        if (_discountReasons.Count == 0)
        {
            problems.Add("Select at least one reason this stock is discounted.");
        }

        if (!_variants.Any(v => v.IsActive))
        {
            problems.Add("Add at least one active variant.");
        }

        if (_options.Any(o => o.Values.Count == 0))
        {
            problems.Add("Every option needs at least one value.");
        }

        if (!_media.Any(m => m.MediaType == ListingMediaType.Product))
        {
            problems.Add("Add at least one product photo.");
        }

        if (DisclosesAPhysicalImperfection(conditionGradeCode, discountReasonCodes)
            && !_media.Any(m => m.MediaType is ListingMediaType.Defect or ListingMediaType.Packaging))
        {
            problems.Add(
                "This listing's condition grade or discount reason discloses a physical imperfection — " +
                "add a defect or packaging photo showing it.");
        }

        return problems;
    }

    /// <summary>Merchant submits a Draft or previously Rejected listing for admin review.</summary>
    public void SubmitForReview(
        string conditionGradeCode, IReadOnlyCollection<string> discountReasonCodes, DateTime nowUtc)
    {
        if (Status is not (ListingStatus.Draft or ListingStatus.Rejected))
        {
            throw new DomainException($"A listing in status {Status} cannot be submitted for review.");
        }

        var blockers = DescribeSubmissionBlockers(conditionGradeCode, discountReasonCodes);
        if (blockers.Count > 0)
        {
            throw new DomainException(blockers[0]);
        }

        Status = ListingStatus.PendingReview;
        SubmittedAtUtc = nowUtc;
        OpenModeration("submitted for review", nowUtc);
        Touch(nowUtc);
    }

    /// <summary>Admin approves the pending version and the listing becomes public.</summary>
    public void Approve(string adminUserId, string? reviewNote, DateTime nowUtc)
    {
        RequirePendingReview();
        PendingModeration!.Resolve(
            ListingModerationStatus.Approved,
            RequireAdminUserId(adminUserId),
            OptionalText(reviewNote, "review note", ListingModeration.MaxReviewNoteLength),
            nowUtc);

        // A listing approved with no sellable stock is published as SoldOut rather than Live:
        // it stays addressable but is not purchasable (docs/03-BUSINESS-RULES.md §2).
        Status = AvailableUnits > 0 ? ListingStatus.Live : ListingStatus.SoldOut;
        PublishedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    /// <summary>Admin rejects the pending version. The merchant can fix it and resubmit.</summary>
    public void Reject(string adminUserId, string reason, DateTime nowUtc)
    {
        RequirePendingReview();
        var note = RequireText(reason, "rejection reason", 1, MaxDecisionReasonLength);
        PendingModeration!.Resolve(
            ListingModerationStatus.Rejected, RequireAdminUserId(adminUserId), note, nowUtc);
        Status = ListingStatus.Rejected;
        PublishedAtUtc = null;
        Touch(nowUtc);
    }

    /// <summary>
    /// Admin takes a published listing out of public view for a policy reason
    /// (docs/04-DOMAIN-MODEL.md §10). Marked distinctly from a merchant's own
    /// <see cref="Hide"/> so the merchant cannot silently reverse an admin takedown through
    /// <see cref="Restore"/> — only <see cref="RestoreByAdmin"/> can lift it
    /// (docs/16-PERMISSIONS-MATRIX.md "Moderate listing — Admin only").
    /// </summary>
    public void HideByAdmin(string adminUserId, string reason, DateTime nowUtc)
    {
        _ = RequireAdminUserId(adminUserId);
        _ = RequireText(reason, "reason", 1, MaxDecisionReasonLength);
        Hide(nowUtc);
        HiddenByAdmin = true;
    }

    /// <summary>Merchant pauses their own published listing without changing what it claims.</summary>
    public void Hide(DateTime nowUtc)
    {
        if (Status is not (ListingStatus.Live or ListingStatus.SoldOut))
        {
            throw new DomainException($"A listing in status {Status} is not published.");
        }

        Status = ListingStatus.Hidden;
        Touch(nowUtc);
    }

    /// <summary>
    /// Merchant republishes their own hidden listing. Only allowed while the last review is
    /// still an approval — a material edit made while hidden returns the listing to Draft, so
    /// this can never restore unreviewed content — and only when an admin did not hide it:
    /// a listing an admin took down for a policy reason can be restored only by
    /// <see cref="RestoreByAdmin"/>, never by the merchant themselves.
    /// </summary>
    public void Restore(DateTime nowUtc)
    {
        RequireHidden();

        if (HiddenByAdmin)
        {
            throw new DomainException(
                "An administrator hid this listing. Contact Faed support to have it restored.");
        }

        RestorePublication(nowUtc);
    }

    /// <summary>
    /// Admin republishes a listing it (or the merchant) hid — the only way to lift an admin
    /// takedown (docs/16-PERMISSIONS-MATRIX.md).
    /// </summary>
    public void RestoreByAdmin(string adminUserId, DateTime nowUtc)
    {
        _ = RequireAdminUserId(adminUserId);
        RequireHidden();
        RestorePublication(nowUtc);
        HiddenByAdmin = false;
    }

    private void RequireHidden()
    {
        if (Status != ListingStatus.Hidden)
        {
            throw new DomainException($"A listing in status {Status} is not hidden.");
        }
    }

    private void RestorePublication(DateTime nowUtc)
    {
        if (LatestModeration is not { Status: ListingModerationStatus.Approved })
        {
            throw new DomainException("This listing has not been approved, so it cannot be published.");
        }

        Status = AvailableUnits > 0 ? ListingStatus.Live : ListingStatus.SoldOut;
        Touch(nowUtc);
    }

    public void Archive(DateTime nowUtc)
    {
        RequireNotArchived();
        Status = ListingStatus.Archived;
        Touch(nowUtc);
    }

    /// <summary>
    /// Reconciles publication with stock after a non-material inventory change: a published
    /// listing with nothing sellable becomes SoldOut, and returns to Live when stock is
    /// replenished (docs/05-USER-FLOWS-AND-STATE-MACHINES.md §2). This never re-opens
    /// moderation — a quantity is not a claim about the product. Uses <see cref="AvailableUnits"/>
    /// computed from the currently loaded <see cref="Variants"/>.
    /// </summary>
    public void RefreshAvailability(DateTime nowUtc) => RefreshAvailability(AvailableUnits, nowUtc);

    /// <summary>
    /// As <see cref="RefreshAvailability(DateTime)"/>, but the caller supplies the current
    /// total available quantity instead of letting it be derived from the loaded
    /// <see cref="Variants"/> collection. Use this when another variant on the same listing
    /// may have been adjusted by a concurrent request since this aggregate was loaded: two
    /// requests each depleting a <em>different</em> variant to zero would otherwise each see
    /// the other variant's stale, still-in-stock value and neither would flip the listing to
    /// SoldOut. The caller is expected to have obtained a fresher total (for example a direct
    /// database sum) immediately before calling this.
    /// </summary>
    public void RefreshAvailability(int currentAvailableUnits, DateTime nowUtc)
    {
        var target = Status switch
        {
            ListingStatus.Live when currentAvailableUnits == 0 => ListingStatus.SoldOut,
            ListingStatus.SoldOut when currentAvailableUnits > 0 => ListingStatus.Live,
            _ => Status,
        };

        if (target == Status)
        {
            return;
        }

        Status = target;
        Touch(nowUtc);
    }

    // ---- Internals ------------------------------------------------------------------

    private void ApplyMaterialChange(string change, DateTime nowUtc)
    {
        switch (Status)
        {
            case ListingStatus.Live:
            case ListingStatus.SoldOut:
                // The published version no longer matches what the merchant is claiming, so it
                // leaves public view until an admin reviews the change (AGENTS.md §8).
                Status = ListingStatus.PendingReview;
                SubmittedAtUtc = nowUtc;
                OpenModeration(change, nowUtc);
                break;

            case ListingStatus.Hidden:
                // A hidden listing is not public, so nothing has to be withdrawn — but the
                // approval it carried no longer describes it. Returning it to Draft forces an
                // explicit resubmission instead of letting Restore republish unreviewed content.
                Status = ListingStatus.Draft;
                break;

            case ListingStatus.PendingReview:
                // Reached only when several material changes are applied in one request; the
                // open moderation record accumulates them.
                PendingModeration?.AppendReason(change);
                break;
        }

        Touch(nowUtc);
    }

    private void OpenModeration(string reason, DateTime nowUtc)
    {
        var pending = PendingModeration;
        if (pending is not null)
        {
            pending.AppendReason(reason);
            return;
        }

        _moderations.Add(new ListingModeration(MerchantProfileId, reason, nowUtc));
    }

    private void ValidateCombination(IReadOnlyCollection<Guid> optionValueIds)
    {
        var distinct = optionValueIds.Distinct().ToList();
        if (distinct.Count != _options.Count)
        {
            throw new DomainException("Choose exactly one value for every option.");
        }

        foreach (var option in _options)
        {
            if (distinct.Count(id => option.Values.Any(v => v.Id == id)) != 1)
            {
                throw new DomainException($"Choose exactly one {option.Name}.");
            }
        }
    }

    private ListingOption FindOption(Guid optionId) =>
        _options.SingleOrDefault(o => o.Id == optionId)
        ?? throw new DomainException("That option is not part of this listing.");

    private ListingVariant FindVariant(Guid variantId) =>
        _variants.SingleOrDefault(v => v.Id == variantId)
        ?? throw new DomainException("That variant is not part of this listing.");

    private void RequireMaterialEditAllowed()
    {
        if (!AcceptsMaterialEdit)
        {
            throw new DomainException(Status == ListingStatus.PendingReview
                ? "This listing is being reviewed and cannot be edited until a decision is made."
                : $"A listing in status {Status} can no longer be edited.");
        }
    }

    private void RequireNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("An archived listing can no longer be changed.");
        }
    }

    private void RequirePendingReview()
    {
        if (Status != ListingStatus.PendingReview || PendingModeration is null)
        {
            throw new DomainException($"A listing in status {Status} is not awaiting moderation.");
        }
    }

    private static string RequireAdminUserId(string adminUserId) =>
        string.IsNullOrWhiteSpace(adminUserId)
            ? throw new DomainException("An admin user id is required to record a moderation decision.")
            : adminUserId;

    private static void Compare(ICollection<string> changes, string field, bool changed)
    {
        if (changed)
        {
            changes.Add(field);
        }
    }

    private static void RequireNonNegative(decimal? value, string field)
    {
        if (value is < 0)
        {
            throw new DomainException($"{field} cannot be negative.");
        }
    }

    private static string RequireText(string value, string field, int minLength, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < minLength)
        {
            throw new DomainException(minLength == 1
                ? $"The listing {field} is required."
                : $"The listing {field} must be at least {minLength} characters.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The listing {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static string? OptionalText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    /// <summary>
    /// Rejects anything but an absolute <c>http</c>/<c>https</c> URL. A reference-price link
    /// is stored and later rendered as a clickable <c>&lt;a href&gt;</c>
    /// (docs/07-UI-UX-SPEC.md §9); an unchecked scheme would let a merchant plant a
    /// <c>javascript:</c> or similarly hostile URL for an admin or buyer to click.
    /// </summary>
    private static string? RequireHttpUrl(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("The reference URL must be a full http:// or https:// link.");
        }

        return value;
    }

    private void Touch(DateTime nowUtc) => UpdatedAtUtc = nowUtc;
}
