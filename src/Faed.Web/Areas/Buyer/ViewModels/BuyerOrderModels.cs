using System.ComponentModel.DataAnnotations;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Buyer.ViewModels;

/// <summary>
/// The buyer's reservation submission: which unit, which pickup point, and the contact
/// details the shop uses to reach them. Prices and the selling merchant are never bound here
/// — they are resolved server-side. A reservation is always one physical unit, collected and
/// paid for in cash at the shop.
/// </summary>
public sealed class ReservationFormModel
{
    public string ListingSlug { get; set; } = string.Empty;

    public Guid VariantId { get; set; }

    public Guid? MerchantLocationId { get; set; }

    [Required(ErrorMessage = "Enter a contact name.")]
    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a contact phone number.")]
    [StringLength(40)]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? BuyerNote { get; set; }
}

public sealed class ReservationPageModel
{
    public required ReservationView Reservation { get; init; }

    public ReservationFormModel Form { get; set; } = new();
}

public sealed class BuyerOrderListPageModel
{
    public required PagedResult<OrderSummaryView> Orders { get; init; }
}

public sealed class BuyerOrderDetailPageModel
{
    public required OrderDetailView Order { get; init; }

    /// <summary>Whether this buyer may review the selling merchant for this order, and any review already left.</summary>
    public ReviewEligibilityView? ReviewEligibility { get; init; }

    public LeaveReviewFormModel ReviewForm { get; set; } = new();
}
