using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Merchant.ViewModels;

public sealed class MerchantDisputeListPageModel
{
    public required PagedResult<DisputeSummaryView> Disputes { get; init; }
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
/// A merchant raising a dispute over a B2C order it sells. The same server-side participant
/// and eligibility checks in <c>DisputeService</c> apply regardless of which surface starts
/// the flow
/// </summary>
public sealed class MerchantFileDisputeFormModel
{
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
    public required Guid TransactionId { get; init; }

    public required string TransactionReference { get; init; }

    /// <summary>The page the "cancel" / breadcrumb link returns to.</summary>
    public required string BackController { get; init; }

    public MerchantFileDisputeFormModel Form { get; set; } = new();
}

public sealed class MerchantReviewsPageModel
{
    public required MerchantReviewHistoryView Reviews { get; init; }
}
