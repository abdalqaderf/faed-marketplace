using System.ComponentModel.DataAnnotations;
using Faed.Web.Models.Enums;
using Faed.Web.Services.Common;
using Faed.Web.Services.Ordering;
using Faed.Web.Services.Trust;

namespace Faed.Web.Areas.Buyer.ViewModels;

/// <summary>One "how many of this variant" row on the checkout form.</summary>
public sealed class CheckoutLineFormModel
{
    public Guid VariantId { get; set; }

    [Range(0, 50, ErrorMessage = "Enter a quantity between 0 and 50.")]
    public int Quantity { get; set; }
}

/// <summary>
/// The buyer's checkout submission. Prices and the selling merchant are never bound here —
/// they are resolved server-side.
/// </summary>
public sealed class CheckoutFormModel
{
    public string ListingSlug { get; set; } = string.Empty;

    public List<CheckoutLineFormModel> Lines { get; set; } = [];

    [Required(ErrorMessage = "Choose how you want to receive the order.")]
    public OrderFulfillmentType FulfillmentType { get; set; } = OrderFulfillmentType.Pickup;

    public Guid? MerchantLocationId { get; set; }

    public Guid? DeliveryZoneId { get; set; }

    [StringLength(600)]
    public string? DeliveryAddressText { get; set; }

    [Required(ErrorMessage = "Enter a contact name.")]
    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a contact phone number.")]
    [StringLength(40)]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? BuyerNote { get; set; }
}

public sealed class CheckoutPageModel
{
    public required CheckoutView Checkout { get; init; }

    public CheckoutFormModel Form { get; set; } = new();
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

    /// <summary>An active (Open/UnderReview) dispute on this order, if there is one.</summary>
    public DisputeSummaryView? ActiveDispute { get; init; }

    /// <summary>Closed disputes on this order, shown as history.</summary>
    public IReadOnlyList<DisputeSummaryView> PastDisputes { get; init; } = [];

    public LeaveReviewFormModel ReviewForm { get; set; } = new();

    public bool CanRaiseDispute { get; init; }
}
