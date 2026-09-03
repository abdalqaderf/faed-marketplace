using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Buyer.ViewModels;

public sealed class BuyerDisputeListPageModel
{
    public required IReadOnlyList<DisputeSummaryView> Disputes { get; init; }
}

public sealed class BuyerDisputeDetailPageModel
{
    public required DisputeDetailView Dispute { get; init; }

    public AddEvidenceFormModel AddEvidence { get; set; } = new();
}

/// <summary>The buyer's "raise a dispute" form for one of their orders.</summary>
public sealed class FileDisputeFormModel
{
    public Guid OrderId { get; set; }

    [Required(ErrorMessage = "Choose a reason.")]
    public DisputeReasonCode ReasonCode { get; set; } = DisputeReasonCode.ItemNotAsDescribed;

    [Required(ErrorMessage = "Describe what went wrong.")]
    [StringLength(Dispute.MaxDescriptionLength, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public List<IFormFile> Evidence { get; set; } = [];
}

public sealed class FileDisputePageModel
{
    public required Guid OrderId { get; init; }

    public required string OrderReference { get; init; }

    public FileDisputeFormModel Form { get; set; } = new();
}

public sealed class AddEvidenceFormModel
{
    public List<IFormFile> Files { get; set; } = [];
}

/// <summary>The buyer's "leave a review" form for a completed order.</summary>
public sealed class LeaveReviewFormModel
{
    [Range(Review.MinRating, Review.MaxRating, ErrorMessage = "Choose a rating from 1 to 5.")]
    public int Rating { get; set; } = 5;

    [StringLength(Review.MaxCommentLength)]
    public string? Comment { get; set; }
}
