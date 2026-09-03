using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class MerchantDisputeListPageModel
{
    public required IReadOnlyList<DisputeSummaryView> Disputes { get; init; }
}

public sealed class MerchantDisputeDetailPageModel
{
    public required DisputeDetailView Dispute { get; init; }

    public MerchantAddEvidenceFormModel AddEvidence { get; set; } = new();
}

public sealed class MerchantAddEvidenceFormModel
{
    public List<IFormFile> Files { get; set; } = [];
}

/// <summary>
/// A merchant raising a dispute over a transaction it takes part in — a wholesale deal it
/// buys or sells, or a B2C order it sells. The same server-side participant and eligibility
/// checks in <c>DisputeService</c> apply regardless of which surface starts the flow
/// (docs/16-PERMISSIONS-MATRIX.md "File eligible dispute — participant ✅").
/// </summary>
public sealed class MerchantFileDisputeFormModel
{
    public TrustTransactionType TransactionType { get; set; } = TrustTransactionType.B2BDeal;

    public Guid TransactionId { get; set; }

    [Required(ErrorMessage = "Choose a reason.")]
    public DisputeReasonCode ReasonCode { get; set; } = DisputeReasonCode.ItemNotAsDescribed;

    [Required(ErrorMessage = "Describe what went wrong.")]
    [StringLength(Dispute.MaxDescriptionLength, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public List<IFormFile> Evidence { get; set; } = [];
}

public sealed class MerchantFileDisputePageModel
{
    public required TrustTransactionType TransactionType { get; init; }

    public required Guid TransactionId { get; init; }

    public required string TransactionReference { get; init; }

    /// <summary>The page the "cancel" / breadcrumb link returns to.</summary>
    public required string BackController { get; init; }

    public MerchantFileDisputeFormModel Form { get; set; } = new();
}

public sealed class MerchantReviewsPageModel
{
    public required MerchantReviewsView Reviews { get; init; }
}

/// <summary>A buying merchant reviewing the seller after a completed wholesale deal.</summary>
public sealed class MerchantLeaveReviewFormModel
{
    [Range(Review.MinRating, Review.MaxRating, ErrorMessage = "Choose a rating from 1 to 5.")]
    public int Rating { get; set; } = 5;

    [StringLength(Review.MaxCommentLength)]
    public string? Comment { get; set; }
}
