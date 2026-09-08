using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Entities;

namespace Faed.Web.Areas.Buyer.ViewModels;

/// <summary>The buyer's "leave a review" form for a completed order.</summary>
public sealed class LeaveReviewFormModel
{
    [Range(Review.MinRating, Review.MaxRating, ErrorMessage = "Choose a rating from 1 to 5.")]
    public int Rating { get; set; } = 5;

    [StringLength(Review.MaxCommentLength)]
    public string? Comment { get; set; }
}
